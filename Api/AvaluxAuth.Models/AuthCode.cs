namespace AvaluxAuth.Models;

public class AuthCode
{
    public required string Code { get; init; }
    public required Guid UserId { get; init; }
    public string? UserNonce { get; init; }
    public DateTimeOffset AuthTime { get; init; }

    // OAuth client binding (H-05): a code may only be redeemed by the client it was
    // issued to, for the same redirect URI, and (for PKCE clients) with the matching verifier.
    public string? ClientId { get; init; }
    public string? RedirectUri { get; init; }
    public string? CodeChallenge { get; init; }
    public string? CodeChallengeMethod { get; init; }

    /// <summary>
    /// Session generation of the user when the code was issued (M-03). A code
    /// issued before a password change or a credential link cannot be exchanged
    /// afterwards. Codes stored before this field existed deserialize as 0, which
    /// is also the initial generation, so pending codes survive the upgrade.
    /// </summary>
    public int SessionVersion { get; init; }
}