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
// M-01: drop avatar uploads that were never attached to an account.
builder.Services.AddHostedService<AvatarCleanupService>();

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
// R-01: password recovery. Tokens live in Redis (hashed, TTL, single use);
// letters are queued and delivered by EmailDispatcher.
builder.Services.AddScoped<IPasswordRecoveryService, PasswordRecoveryService>();
builder.Services.AddSingleton<IPasswordResetTokenRepository, RedisPasswordResetTokenRepository>();
builder.Services.AddSingleton<IEmailQueue, EmailQueue>();
builder.Services.AddHostedService<EmailDispatcher>();
builder.Services.AddAuthProviders();

// R-01: fail closed. Recovery is enabled only by an explicit mail backend; the
// `Log` sender exists for disposable stands and writes reset links to the log.
var emailMode = (builder.Configuration["Email:Mode"] ?? "Disabled").Trim();
if (emailMode.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
else if (emailMode.Equals("Log", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IEmailSender, LogEmailSender>();
else
    builder.Services.AddSingleton<IEmailSender, DisabledEmailSender>();

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
            //
            // User access tokens are stateless, but they carry the user's session
            // version; a password change or a newly linked credential bumps it and
            // every token issued before stops validating (M-03 in SECURITY-AUDIT.md).
            OnTokenValidated = async context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("AvaluxAuth.Api.TokenValidation");

                var tokenIdClaim = context.Principal?.FindFirst("TokenId");
                if (tokenIdClaim is null)
                {
                    var userIdClaim = context.Principal?.FindFirst("UserId");
                    if (userIdClaim is null)
                        return; // id_token or a token without a user binding

                    if (!Guid.TryParse(userIdClaim.Value, out var userId))
                    {
                        context.Fail("User access token has an invalid UserId claim");
                        return;
                    }

                    // Tokens issued before the claim existed are generation 0, which
                    // is also the initial column default: the migration logs nobody out.
                    var sessionVersion = int.TryParse(context.Principal?.FindFirst("SessionVersion")?.Value,
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedVersion)
                        ? parsedVersion
                        : 0;

                    try
                    {
                        var userRepository = context.HttpContext.RequestServices
                            .GetRequiredService<IUserRepository>();
                        var currentVersion = await userRepository.GetSessionVersionAsync(userId,
                            context.HttpContext.RequestAborted);
                        if (currentVersion != sessionVersion)
                        {
                            logger.LogWarning(
                                "Rejected access token with a stale session version: userId={UserId}, tokenVersion={TokenVersion}",
                                userId, sessionVersion);
                            context.Fail("Session has been revoked");
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Session version check failed: userId={UserId}", userId);
                        context.Fail("Session version check failed");
                    }

                    return;
                }

                if (!Guid.TryParse(tokenIdClaim.Value, out var tokenId))
                {
                    context.Fail("Service account token has an invalid TokenId claim");
                    return;
                }

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

    // R-01: recovery is anonymous and sends e-mail, so both endpoints are capped
    // per client IP. The per-account cooldown in Redis complements this.
    options.AddPolicy(Config.PasswordRecoverRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.GetClientIpAddress(),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));

    options.AddPolicy(Config.PasswordResetCompleteRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.GetClientIpAddress(),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));

    // M-01: the avatar upload is reachable before sign-in, so it is capped per
    // IP as well. The per-state quota counts successful states, this limits how
    // many states one client can burn through.
    options.AddPolicy(Config.AvatarUploadRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.GetClientIpAddress(),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));

    // Application icons: admin-only, but one IP cannot hammer S3 through the
    // panel with uploads either.
    options.AddPolicy(Config.IconUploadRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.GetClientIpAddress(),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(10),
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

// R-01: say out loud whether recovery is active, so a missing SMTP variable is
// visible in the container log instead of surfacing as a silent 503.
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("AvaluxAuth.Api.Startup");
if (emailMode.Equals("Smtp", StringComparison.OrdinalIgnoreCase) ||
    emailMode.Equals("Log", StringComparison.OrdinalIgnoreCase) ||
    emailMode.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
{
    startupLogger.LogInformation("Email delivery mode: {Mode}", emailMode);
}
else
{
    startupLogger.LogError("Unknown Email:Mode value: {Mode}; e-mail delivery is disabled", emailMode);
}

if (emailMode.Equals("Log", StringComparison.OrdinalIgnoreCase))
    startupLogger.LogWarning("Email:Mode=Log writes password reset links to the log; use it only on a disposable stand");

await using (var startupScope = app.Services.CreateAsyncScope())
{
    var recovery = startupScope.ServiceProvider.GetRequiredService<IPasswordRecoveryService>();
    startupLogger.LogInformation("Password recovery enabled: {Enabled}", recovery.IsEnabled);
}

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
            (path == "/login" || path == "/login/" || path == "/profile" || path == "/profile/" ||
             path == "/recover" || path == "/recover/"))
        {
            context.Request.Path = "/index.html";
            // The page can carry a one-time token in the fragment; never let a
            // proxy or browser reuse a cached copy of it.
            context.Response.Headers.CacheControl = "no-store";
        }

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