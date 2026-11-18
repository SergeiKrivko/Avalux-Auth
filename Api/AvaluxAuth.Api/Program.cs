using System.Globalization;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Api;
using AvaluxAuth.DataAccess;
using AvaluxAuth.DataAccess.Repositories;
using AvaluxAuth.Providers.Extensions;
using AvaluxAuth.S3;
using AvaluxAuth.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddScoped<IApplicationRepository, ApplicationRepository>();
builder.Services.AddScoped<IProviderRepository, ProviderRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddSingleton<IStateRepository, RedisStateRepository>();
builder.Services.AddSingleton<IAuthCodeRepository, RedisCodeRepository>();
builder.Services.AddSingleton<ILinkCodeRepository, RedisLinkCodeRepository>();
builder.Services.AddSingleton<ILoginThrottle, RedisLoginThrottle>();
builder.Services.AddScoped<ISigningKeyRepository, SigningKeyRepository>();
builder.Services.AddScoped<ITokenRepository, TokenRepository>();
builder.Services.AddScoped<IPasswordRepository, PasswordRepository>();
builder.Services.AddSingleton<IFileRepository, S3Repository>();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["Security:KeysPath"] ?? "./keys"));
builder.Services.AddSingleton<ISecretProtector, SecretProtector>();

builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<IOauthService, OauthService>();
builder.Services.AddScoped<IAuthorizationService, AuthorizationService>();
builder.Services.AddScoped<ISigningKeyService, SigningKeyService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddSingleton<IImageService, ImageService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddAuthProviders();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<AvaluxAuthDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration["ConnectionStrings:Postgres"]);
});
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var redisConfiguration = ConfigurationOptions.Parse(
        builder.Configuration["ConnectionStrings:Redis"] ?? "localhost:6379");

    // H-07 in SECURITY-AUDIT.md: Redis is reached over TLS on the internal
    // network with a certificate from a private CA, so the system trust store
    // cannot validate it. When Security:RedisCaCertificatePath is set, verify
    // the server chain against exactly that CA (plus the TLS hostname check).
    var caCertificatePath = builder.Configuration["Security:RedisCaCertificatePath"];
    if (!string.IsNullOrWhiteSpace(caCertificatePath))
    {
        var caCertificate = X509CertificateLoader.LoadCertificateFromFile(caCertificatePath);
        redisConfiguration.CertificateValidation += (_, certificate, _, errors) =>
        {
            if (certificate is null || (errors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
                return false;

            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(caCertificate);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return chain.Build(X509CertificateLoader.LoadCertificate(certificate.GetRawCertData()));
        };
    }

    return ConnectionMultiplexer.Connect(redisConfiguration);
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Path = "/";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    })
    .AddJwtBearer(options =>
    {
        options.MetadataAddress =
            $"{builder.Configuration["Api:ApiUrl:Local"]}/api/v1/.well-known/openid-configuration";
        options.Authority = builder.Configuration["Security:AuthApiUrl"];
        options.RequireHttpsMetadata = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Security:Issuer"],
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
        };

        options.RefreshInterval = TimeSpan.FromMinutes(30);
        options.RefreshOnIssuerKeyNotFound = true;

        options.Events = new JwtBearerEvents
        {
            // Service account JWTs may live for years, so a signature/expiry check
            // alone is not enough: a token revoked in the admin panel must stop
            // working immediately (H-04 in SECURITY-AUDIT.md). Fail closed: if the
            // revocation status cannot be checked, the token is rejected.
            OnTokenValidated = async context =>
            {
                var tokenIdClaim = context.Principal?.FindFirst("TokenId");
                if (tokenIdClaim is null)
                    return; // user access token: no DB-backed record (see M-03)

                if (!Guid.TryParse(tokenIdClaim.Value, out var tokenId))
                {
                    context.Fail("Service account token has an invalid TokenId claim");
                    return;
                }

                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("AvaluxAuth.Api.ServiceAccountTokens");

                try
                {
                    var tokenService = context.HttpContext.RequestServices.GetRequiredService<ITokenService>();
                    if (await tokenService.IsRevokedAsync(tokenId, context.HttpContext.RequestAborted))
                    {
                        logger.LogWarning("Rejected revoked service account token: tokenId={TokenId}", tokenId);
                        context.Fail("Service account token has been revoked");
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Service account token revocation check failed: tokenId={TokenId}", tokenId);
                    context.Fail("Service account token revocation check failed");
                }
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Config.AdminPolicy, policy => policy.RequireRole(Config.AdminRole))
    .AddPolicy(Config.UserPolicy, policy => policy.RequireClaim("UserId"))
    .AddPolicy(Config.AdminOrServiceAccountPolicy,
        policy => { policy.RequireRole(Config.AdminRole, Config.ServiceAccountRole); })
    .AddPolicy(Config.ServiceAccountPolicy, policy =>
    {
        policy.RequireRole(Config.ServiceAccountRole);
        policy.RequireClaim("ApplicationId");
        policy.RequireClaim("TokenId");
        policy.RequireClaim("Permissions");
    });

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader()
    );
    options.AddPolicy(Config.AdminPolicy, policy => policy
        .WithOrigins("http://localhost:4200")
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()
    );
});

// H-06: brute-force protection. The ASP.NET Core limiter is a cheap per-IP
// first line of defence on the password and admin-login endpoints; the
// Redis-backed ILoginThrottle adds a per-(IP, login) exponential lockout that
// also survives restarts and counts failures across the state lifetime.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(Config.AuthRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.GetClientIpAddress(),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));

    options.AddPolicy(Config.AdminLoginRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.GetClientIpAddress(),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                    .ToString(CultureInfo.InvariantCulture);
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"error\":\"too_many_requests\",\"message\":\"Too many attempts. Try again later.\"}", ct);
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Authorization with API token",
        Name = "Token Auth",
        Scheme = "Bearer",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
    });
});

var app = builder.Build();

app.UseRouting();

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
if (app.Environment.IsProduction())
{
    // The user-facing auth UI (Svelte) lives in the wwwroot root and is
    // served at /login and /profile. Requests are rewritten to index.html,
    // there are no redirects.
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path;

        if ((HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)) &&
            (path == "/login" || path == "/login/" || path == "/profile" || path == "/profile/"))
            context.Request.Path = "/index.html";

        await next();
    });

    app.UseDefaultFiles();
    app.UseStaticFiles();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<AvaluxAuthDbContext>().Database.MigrateAsync();
}

app.Run();