using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using AvaluxAuth.Utils;
using Microsoft.Extensions.Configuration;

namespace AvaluxAuth.Providers;

public class PasswordAuthProvider(IConfiguration configuration) : IAuthProvider
{
    public string Name => "Логин и пароль";
    public string Key => "password";
    public int Id => 0;

    public string GetAuthUrl(ProviderParameters parameters, string redirectUrl, string state)
    {
        throw new NotSupportedException("Not available for password authorization");
    }

    public Task<AccountCredentials> GetTokenAsync(ProviderParameters parameters,
        Dictionary<string, string> queryParameters, string redirectUrl, CancellationToken ct)
    {
        throw new NotSupportedException("Not available for password authorization");
    }

    public Task<AccountCredentials> RefreshTokenAsync(ProviderParameters parameters, string refreshToken,
        CancellationToken ct)
    {
        throw new NotSupportedException("Not available for password authorization");
    }

    public Task<bool> RevokeTokenAsync(ProviderParameters parameters, AccountCredentials credentials,
        CancellationToken ct = default)
    {
        throw new NotSupportedException("Not available for password authorization");
    }

    public Task<UserInfo> GetUserInfoAsync(ProviderParameters parameters, AccountCredentials credentials,
        CancellationToken ct)
    {
        throw new NotSupportedException("Not available for password authorization");
    }
}