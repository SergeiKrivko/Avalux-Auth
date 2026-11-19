using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

/// <summary>
/// One-time password reset tokens (R-01 in SECURITY-AUDIT.md) and the delivery
/// cooldown that keeps the endpoint from being used as a mail bomb.
/// </summary>
public interface IPasswordResetTokenRepository
{
    Task SaveAsync(string tokenHash, PasswordResetToken token, TimeSpan lifetime);

    /// <summary>
    /// Atomically reads and deletes the token. Returns <c>null</c> when the token
    /// is unknown, expired or was already used, so concurrent resets cannot both
    /// consume it.
    /// </summary>
    Task<PasswordResetToken?> TakeAsync(string tokenHash);

    /// <summary>
    /// Reserves one delivery for the account. Returns <c>false</c> when the
    /// per-account cooldown or the hourly cap is in effect; the caller then skips
    /// the letter but keeps the response generic.
    /// </summary>
    Task<bool> TryReserveSendAsync(Guid passwordId, CancellationToken ct = default);
}
