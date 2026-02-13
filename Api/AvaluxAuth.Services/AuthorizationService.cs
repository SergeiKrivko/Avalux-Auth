using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using AvaluxAuth.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AvaluxAuth.Services;

public class AuthorizationService(
    IProviderFactory providerFactory,
    IApplicationRepository applicationRepository,
    IProviderRepository providerRepository,
    IStateRepository stateRepository,
    IAuthCodeRepository authCodeRepository,
    IConfiguration configuration,
    IAccountRepository accountRepository,
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ISigningKeyService signingKeyService,
    ISecretProtector secretProtector) : IAuthorizationService
{
    public async Task<string> GetAuthUrlAsync(string clientId, string providerKey, string redirectUrl,
        CancellationToken ct = default)
    {
        var application = await applicationRepository.GetApplicationByClientIdAsync(clientId, ct);
        if (application == null)
            throw new Exception("Client not found");

        if (!application.Parameters.RedirectUrls.Contains(redirectUrl))
            throw new Exception("Invalid redirect url");

        if (!providerFactory.TryGetProvider(providerKey, out var provider))
            throw new Exception("Provider not found");
        var p = await providerRepository.GetProviderByProviderIdAsync(application.Id, provider.Id, ct);
        if (p == null)
            throw new Exception("Provider is not added to this application");

        var state = RandomNumberGenerator.GetRandomString(64);
        await stateRepository.SaveStateAsync(new AuthorizationState
        {
            State = state,
            ApplicationId = application.Id,
            ProviderId = p.Id,
            RedirectUrl = redirectUrl
        });

        return provider.GetAuthUrl(p.Parameters, GetCallbackUrl(provider.Key), state);
    }

    public async Task<string> ExchangeCredentialsAsync(Dictionary<string, string> query, string state,
        CancellationToken ct = default)
    {
        var parameters = await stateRepository.TakeStateAsync(state);
        var p = await providerRepository.GetProviderByIdAsync(parameters.ProviderId, ct);
        if (p == null)
            throw new Exception("Provider not found");
        if (!providerFactory.TryGetProvider(p.ProviderId, out var provider))
            throw new Exception("Provider not found");
        var credentials = await provider.GetTokenAsync(p.Parameters, query, GetCallbackUrl(provider.Key), ct);
        var info = await provider.GetUserInfoAsync(credentials, ct);

        var account = await accountRepository.GetAccountByProviderIdAsync(parameters.ApplicationId, info.Id, ct);
        Guid accountId;
        Guid userId;
        if (account == null)
        {
            userId = await userRepository.CreateUserAsync(p.ApplicationId, ct);
            accountId = await accountRepository.CreateAccountAsync(userId, p.Id, info, credentials, ct);
        }
        else
        {
            userId = account.UserId;
            accountId = account.Id;
            await accountRepository.UpdateAccountTokensAsync(accountId, credentials, ct);
        }

        var code = RandomNumberGenerator.GetRandomString();
        await authCodeRepository.SaveCodeAsync(new AuthCode
        {
            Code = code,
            UserId = userId,
            AccountId = accountId,
        });
        return new UrlBuilder(parameters.RedirectUrl)
            .AddQuery("code", code)
            .ToString();
    }

    public async Task<bool> CheckClientSecretAsync(string clientId, string clientSecret, CancellationToken ct = default)
    {
        var application = await applicationRepository.GetApplicationByClientIdAsync(clientId, ct);
        if (application == null)
            throw new Exception("Client not found");
        return application.ClientSecret == clientSecret;
    }

    public async Task<UserCredentials> AuthorizeUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepository.GetUserAsync(userId, ct);
        if (user == null)
            throw new Exception("User not found");
        var application = await applicationRepository.GetApplicationByIdAsync(user.ApplicationId, ct);
        if (application == null)
            throw new Exception("Application not found");

        var expiresAt = DateTime.UtcNow.AddHours(1);
        var accessToken = await CreateJwt(user, application, expiresAt, ct);
        var refreshToken = await CreateRefreshToken(user, ct);
        return new UserCredentials
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
        };
    }

    private async Task<SigningCredentials> GetSecurityKeyAsync(CancellationToken ct = default)
    {
        var key = await signingKeyService.GetActiveSigningKeyAsync(ct);

        var rsa = RSA.Create();
        var privateKey = Convert.FromBase64String(secretProtector.Unprotect(key.PrivateKeyEncrypted));
        rsa.ImportPkcs8PrivateKey(privateKey, out _);

        var securityKey = new RsaSecurityKey(rsa)
        {
            KeyId = key.Kid
        };
        return new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);
    }


    private async Task<string> CreateJwt(User user, Application application, DateTime expiresAt,
        CancellationToken ct = default)
    {
        var jwt = new JwtSecurityToken(
            issuer: configuration["Security.Issuer"],
            audience: application.Parameters.Name,
            claims:
            [
                new Claim("UserId", user.Id.ToString())
            ],
            expires: expiresAt,
            signingCredentials: await GetSecurityKeyAsync(ct)
        );
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private async Task<string> CreateRefreshToken(User user, CancellationToken ct = default)
    {
        var refreshToken = RandomNumberGenerator.GetRandomString(128);
        await refreshTokenRepository.AddRefreshTokenAsync(refreshToken, user.Id, ct);
        return refreshToken;
    }

    public async Task<UserCredentials?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var newToken = RandomNumberGenerator.GetRandomString(128);
        var userId = await refreshTokenRepository.ReplaceRefreshTokenAsync(refreshToken, newToken, ct);
        if (userId is null)
            return null;
        var user = await userRepository.GetUserAsync(userId.Value, ct);
        if (user is null)
            return null;
        var application = await applicationRepository.GetApplicationByIdAsync(user.ApplicationId, ct);
        var expiresAt = DateTime.UtcNow.AddHours(1);
        var accessToken = await CreateJwt(user, application ?? throw new Exception("Application not found"), expiresAt,
            ct);
        return new UserCredentials
        {
            AccessToken = accessToken,
            RefreshToken = newToken,
            ExpiresAt = expiresAt,
        };
    }

    private string GetCallbackUrl(string providerKey)
    {
        return
            $"{configuration["Api.ApiUrl"] ?? throw new Exception("Api url not found")}/api/v1/auth/{providerKey}/callback";
    }
}