using System.Text;

namespace AvaluxAuth.Services;

/// <summary>
/// Server-side password policy (H-06 in SECURITY-AUDIT.md). The auth UI validates
/// the same rules, but the API must not rely on the client.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    // BCrypt silently ignores everything past 72 bytes. Reject longer passwords
    // instead of truncating them during hashing.
    public const int MaxUtf8Bytes = 72;

    /// <returns>A human-readable error, or <c>null</c> when the password is acceptable.</returns>
    public static string? Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return "Password is required";

        if (password.Length < MinLength)
            return $"Password must be at least {MinLength} characters long";

        if (Encoding.UTF8.GetByteCount(password) > MaxUtf8Bytes)
            return $"Password must not exceed {MaxUtf8Bytes} bytes";

        return null;
    }
}
