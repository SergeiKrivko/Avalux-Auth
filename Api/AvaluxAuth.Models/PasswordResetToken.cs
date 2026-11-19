namespace AvaluxAuth.Models;

/// <summary>
/// Server-side record of a pending password reset (R-01 in SECURITY-AUDIT.md).
/// The token itself is never stored: the Redis key is its SHA-256 hash, so a
/// cache dump alone cannot be used to reset a password.
/// </summary>
public class PasswordResetToken
{
    public required Guid PasswordId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
