using AvaluxAuth.Abstractions;
using AvaluxAuth.Api;
using AvaluxAuth.DataAccess;
using AvaluxAuth.DataAccess.Repositories;
using AvaluxAuth.Providers.Extensions;
using AvaluxAuth.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddScoped<IApplicationRepository, ApplicationRepository>();
builder.Services.AddScoped<IProviderRepository, ProviderRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddSingleton<IStateRepository, InMemoryStateRepository>();
builder.Services.AddSingleton<IAuthCodeRepository, InMemoryCodeRepository>();
builder.Services.AddScoped<ISigningKeyRepository, SigningKeyRepository>();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["Security.KeysPath"] ?? "./keys"));
builder.Services.AddSingleton<ISecretProtector, SecretProtector>();

builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<IAuthorizationService, AuthorizationService>();
builder.Services.AddScoped<ISigningKeyService, SigningKeyService>();
builder.Services.AddAuthProviders();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<AvaluxAuthDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration["ConnectionStrings.Postgres"]);
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
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Config.AdminPolicy, policy => policy.RequireRole(Config.AdminRole));
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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
if (app.Environment.IsProduction())
{
    app.UseStaticFiles();
    app.UseSpa(spa =>
    {
        spa.Options.SourcePath = "wwwroot";
        spa.Options.DefaultPage = "/index.html";
    });
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
    await scope.ServiceProvider.GetRequiredService<ISigningKeyService>().RotateSigningKeyAsync();
}

app.Run();