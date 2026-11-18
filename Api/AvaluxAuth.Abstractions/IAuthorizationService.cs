using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IAuthorizationService
{
    public Task<string> CreateAuthorizationCodeAsync(Guid userId, string clientId, string redirectUri,
        string? nonce, string? codeChallenge, string? codeChallengeMethod, CancellationToken ct = default);

    /// <summary>
    /// Redeems an authorization code. The code is bound to <paramref name="clientId"/> and
    /// <paramref name="redirectUri"/>, and (for PKCE clients) to the verifier matching the
    /// stored <c>code_challenge</c>. Returns <c>null</c> on any mismatch (H-05).
    /// </summary>
    public Task<UserCredentials?> GetTokenAsync(string code, string clientId, string? codeVerifier,
        string? redirectUri, CancellationToken ct = default);

    public Task<UserCredentials?> RefreshTokenAsync(string refreshToken, string clientId,
        CancellationToken ct = default);

    public Task<bool> RevokeTokenAsync(string refreshToken, string clientId, CancellationToken ct = default);
}
