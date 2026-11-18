using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IOauthService
{
    public Task<string> CreateLinkCode(Guid userId, CancellationToken ct = default);

    public Task<string> GetAuthUrlAsync(string clientId, string providerKey, string redirectUri,
        string? state, string? nonce, string? linkCode, string? codeChallenge, string? codeChallengeMethod,
        CancellationToken ct = default);

    public Task<ProcessedAuthorizationState> ProcessCodeAsync(Dictionary<string, string> query, string state,
        CancellationToken ct = default);

    /// <summary>
    /// Authenticates an OAuth client at the token endpoint. Public clients (no client secret)
    /// always pass; confidential clients must present the exact client secret (H-05).
    /// </summary>
    public Task<bool> AuthenticateClientAsync(string clientId, string? clientSecret,
        CancellationToken ct = default);
}
