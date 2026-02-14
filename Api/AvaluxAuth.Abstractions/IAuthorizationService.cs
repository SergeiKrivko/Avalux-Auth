using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IAuthorizationService
{
    public Task<string> GetAuthUrlAsync(string clientId, string providerKey, string redirectUrl,
        CancellationToken ct = default);

    public Task<string> SaveCodeAsync(Dictionary<string, string> query, string state,
        CancellationToken ct = default);

    public Task<bool> CheckClientSecretAsync(string clientId, string clientSecret,
        CancellationToken ct = default);

    public Task<UserCredentials> AuthorizeUserAsync(string code, CancellationToken ct = default);
    public Task LinkAccountAsync(Guid userId, string code, CancellationToken ct = default);
    public Task<UserCredentials?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
}