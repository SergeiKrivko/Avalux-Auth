using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IAuthorizationService
{
    public Task<string> GetAuthUrlAsync(string clientId, string providerKey, string redirectUrl, Guid? userId = null,
        CancellationToken ct = default);

    public Task<string> ExchangeCredentialsAsync(Dictionary<string, string> query, string state,
        CancellationToken ct = default);

    public Task<bool> CheckClientSecretAsync(string clientId, string clientSecret,
        CancellationToken ct = default);

    public Task<UserCredentials> AuthorizeUserAsync(Guid userId, CancellationToken ct = default);
    public Task<UserCredentials?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
}