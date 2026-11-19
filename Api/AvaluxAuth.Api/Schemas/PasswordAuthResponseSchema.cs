namespace AvaluxAuth.Api.Schemas;

public class PasswordAuthResponseSchema
{
    public required string RedirectUrl { get; init; }

    /// <summary>
    /// R-02 in SECURITY-AUDIT.md: set only after sign-up, when an e-mail
    /// confirmation letter was queued. The page offers this redirect as the skip
    /// action, so confirmation never blocks sign-in.
    /// </summary>
    public EmailConfirmationSchema? EmailConfirmation { get; init; }
}