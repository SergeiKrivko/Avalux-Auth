namespace AvaluxAuth.Models;

/// <summary>
/// A plain-text e-mail queued for delivery (R-01 in SECURITY-AUDIT.md). HTML is
/// deliberately not supported: there is no remote content or tracking, and the
/// body of a password reset letter is a link the user can read.
/// </summary>
public class EmailMessage
{
    public required string To { get; init; }
    public required string Subject { get; init; }
    public required string TextBody { get; init; }
}
