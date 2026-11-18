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
}