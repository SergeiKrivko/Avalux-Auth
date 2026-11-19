using MimeKit;

namespace AvaluxAuth.Services;

/// <summary>
/// Validation of the e-mail address stored on a password account (R-01 in
/// SECURITY-AUDIT.md). The address is the only delivery channel of the recovery
/// flow, so the API does not accept a value that cannot be delivered or that
/// does not fit the columns.
/// </summary>
public static class EmailPolicy
{
    /// <summary>
    /// Maximum stored length. Sign-up writes the same value to
    /// <c>Passwords.Email</c> (64) and <c>Accounts.Email</c> (50); the tighter
    /// column is the effective limit and rejecting longer values here turns a
    /// database error (500) into a clear client error.
    /// </summary>
    public const int MaxLength = 50;

    /// <returns>A human-readable error, or <c>null</c> when the address is acceptable.</returns>
    public static string? Validate(string? email)
    {
        // The address stays optional: recovery is unavailable without it, but
        // registration does not require one.
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var trimmed = email.Trim();
        if (trimmed.Length > MaxLength)
            return $"Email must not exceed {MaxLength} characters";

        // Reject display-name forms ("Name <user@example.com>") and anything the
        // address parser does not fully consume.
        if (!MailboxAddress.TryParse(trimmed, out var mailbox) ||
            mailbox is null ||
            !string.IsNullOrEmpty(mailbox.Name) ||
            !string.Equals(mailbox.Address, trimmed, StringComparison.Ordinal))
        {
            return "Invalid email address";
        }

        return null;
    }
}
