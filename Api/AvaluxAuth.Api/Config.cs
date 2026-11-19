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

    // M-01: anonymous avatar uploads. Uploads require a live authorization state
    // and are additionally capped per state and per client IP.
    public const string AvatarUploadRateLimitPolicy = "AvatarUpload";
    public const int MaxAvatarUploadsPerState = 5;

    // R-01: password recovery. Both endpoints are anonymous, so they get their
    // own per-IP windows on top of the per-account delivery cooldown in Redis.
    public const string PasswordRecoverRateLimitPolicy = "PasswordRecover";
    public const string PasswordResetCompleteRateLimitPolicy = "PasswordResetComplete";
    public const int MaxRecoveryIdentifierLength = 254;

    // 32 random bytes, hex encoded. The controller rejects anything else before
    // Redis is queried.
    public const int ResetTokenLength = 64;

    // Scopes of the failed-login counters in Redis.
    public const string PasswordSignInScope = "password-signin";
    public const string PasswordSignUpScope = "password-signup";
    public const string AdminLoginScope = "admin-login";

    // A single authorization state allows at most this many failed attempts.
    public const int MaxFailedAttemptsPerState = 10;
}