using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

/// <summary>
/// Single-use e-mail confirmation tickets (R-02 in SECURITY-AUDIT.md). Keys hold
/// the SHA-256 of the token and carry a TTL, so an unused link disappears on its
/// own.
/// </summary>
public interface IEmailConfirmationRepository
{
    Task SaveAsync(string tokenHash, EmailConfirmationTicket ticket, TimeSpan lifetime);

    /// <summary>
    /// Atomically reads and deletes the ticket. Returns <c>null</c> when the link
    /// is unknown, expired or was already used, so two concurrent confirmations
    /// cannot both redeem it.
    /// </summary>
    Task<EmailConfirmationTicket?> TakeAsync(string tokenHash);
}
