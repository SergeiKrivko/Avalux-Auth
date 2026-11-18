namespace AvaluxAuth.Api;

public static class Config
{
    public const string AdminRole = "Admin";
    public const string ServiceAccountRole = "ServiceAccount";

    public const string AdminPolicy = "Admin";
    public const string PublicPolicy = "Public";
    public const string UserPolicy = "User";
    public const string AdminOrServiceAccountPolicy = "AdminOrServiceAccount";
    public const string ServiceAccountPolicy = "ServiceAccount";

    // Rate limiting (H-06). Policy names are referenced by [EnableRateLimiting].
    public const string AuthRateLimitPolicy = "AuthEndpoints";
    public const string AdminLoginRateLimitPolicy = "AdminLogin";

    // Scopes of the failed-login counters in Redis.
    public const string PasswordSignInScope = "password-signin";
    public const string PasswordSignUpScope = "password-signup";
    public const string AdminLoginScope = "admin-login";

    // A single authorization state allows at most this many failed attempts.
    public const int MaxFailedAttemptsPerState = 10;
}