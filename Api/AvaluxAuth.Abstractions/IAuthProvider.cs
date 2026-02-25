using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IAuthProvider
{
    public string Name { get; }
    public string Key { get; }
    public int Id { get; }
    public string? ProviderUrl => null;
    public string[] Fields => [nameof(ProviderParameters.ClientId), nameof(ProviderParameters.ClientSecret)];

    public string GetAuthUrl(ProviderParameters parameters, string redirectUrl, string state);

    public Task<AccountCredentials> GetTokenAsync(ProviderParameters parameters,
        Dictionary<string, string> queryParameters, string redirectUrl, CancellationToken ct);

    public Task<AccountCredentials> RefreshTokenAsync(ProviderParameters parameters, string refreshToken,
        CancellationToken ct);

    public Task<bool> RevokeTokenAsync(ProviderParameters parameters, AccountCredentials credentials,
        CancellationToken ct = default);

    public Task<UserInfo> GetUserInfoAsync(ProviderParameters parameters, AccountCredentials credentials,
        CancellationToken ct);
}