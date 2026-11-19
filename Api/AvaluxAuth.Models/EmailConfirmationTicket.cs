namespace AvaluxAuth.Models;

/// <summary>
/// Server-side state of an issued e-mail confirmation link (R-02 in
/// SECURITY-AUDIT.md). Redis keeps only the SHA-256 of the token and this
/// ticket, so a cache dump cannot be replayed as a working link.
/// </summary>
public class EmailConfirmationTicket
{
    public required Guid PasswordId { get; init; }
    public required Guid UserId { get; init; }
    public required string Email { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// The authorization request that registration interrupted. When present,
    /// redeeming the link mints a fresh code and redirects straight back to the
    /// client, so the user is not asked for credentials again. Null for a link
    /// sent from the profile page.
    /// </summary>
    public EmailConfirmationAuthorization? Authorization { get; init; }
}

/// <summary>
/// Copy of the authorization state that is needed to finish sign-in after the
/// confirmation link is followed. Only fields the redirect and the code need are
/// kept; the password and the address never live here.
/// </summary>
public class EmailConfirmationAuthorization
{
    public required string ClientId { get; init; }
    public required string RedirectUrl { get; init; }
    public string? UserState { get; init; }
    public string? UserNonce { get; init; }
    public string? CodeChallenge { get; init; }
    public string? CodeChallengeMethod { get; init; }
}
