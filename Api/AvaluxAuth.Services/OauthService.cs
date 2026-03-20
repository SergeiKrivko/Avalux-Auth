using System.Security.Cryptography;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using AvaluxAuth.Utils;
using Microsoft.Extensions.Configuration;

namespace AvaluxAuth.Services;

public class OauthService(
    IProviderFactory providerFactory,
    IApplicationRepository applicationRepository,
    IProviderRepository providerRepository,
    IStateRepository stateRepository,
    ILinkCodeRepository linkCodeRepository,
    IAccountRepository accountRepository,
    IUserRepository userRepository,
    ISecretProtector secretProtector,
    IConfiguration configuration,
    IImageService imageService) : IOauthService
{
    public async Task<string> CreateLinkCode(Guid userId, CancellationToken ct = default)
    {
        var linkCode = RandomNumberGenerator.GetRandomString(64);
        await linkCodeRepository.SaveCodeAsync(new LinkCode
        {
            Code = linkCode,
            UserId = userId,
        });
        return linkCode;
    }

    public async Task<string> GetAuthUrlAsync(string clientId, string providerKey, string redirectUri,
        string? userState, string? userNonce, string? linkCode,
        CancellationToken ct = default)
    {
        var application = await applicationRepository.GetApplicationByClientIdAsync(clientId, ct);
        if (application == null)
            throw new Exception("Client not found");

        if (!application.Parameters.RedirectUrls.Contains(redirectUri))
            throw new Exception("Invalid redirect url");

        if (!providerFactory.TryGetProvider(providerKey, out var provider))
            throw new Exception("Provider not found");
        var providerSettings = await providerRepository.GetProviderByProviderIdAsync(application.Id, provider.Id, ct);
        if (providerSettings == null)
            throw new Exception("Provider is not added to this application");

        var link = linkCode == null ? null : await linkCodeRepository.TakeCodeAsync(linkCode);
        Console.WriteLine(link == null);
        var existingAccount = link == null
            ? null
            : (await accountRepository.GetAccountsOfUserAsync(link.UserId, ct)).FirstOrDefault(e =>
                e.ProviderId == providerSettings.Id);
        Console.WriteLine(existingAccount == null);
        if (existingAccount != null && provider.Id != 0)
            throw new Exception("Account of this provider is linked already");

        var state = RandomNumberGenerator.GetRandomString(64);
        await stateRepository.SaveStateAsync(new AuthorizationState
        {
            State = state,
            UserState = userState,
            UserNonce = userNonce,
            LinkUserId = link?.UserId,
            ApplicationId = application.Id,
            ProviderId = providerSettings.Id,
            RedirectUrl = redirectUri,
        });

        if (existingAccount != null)
            return $"{configuration["Api.ApiUrl"]}/profile?state={state}";

        return provider.Id == 0
            ? $"{configuration["Api.ApiUrl"]}/login?state={state}"
            : provider.GetAuthUrl(providerSettings.Parameters, GetCallbackUrl(provider.Key), state);
    }

    private string GetCallbackUrl(string providerKey)
    {
        return
            $"{configuration["Api.ApiUrl"] ?? throw new Exception("Api url not found")}/api/v1/auth/{providerKey}/callback";
    }

    public async Task<ProcessedAuthorizationState> ProcessCodeAsync(Dictionary<string, string> query,
        string stateString,
        CancellationToken ct = default)
    {
        var state = await stateRepository.TakeStateAsync(stateString);
        if (state is null)
            throw new Exception("State not found");

        var providerSettings = await providerRepository.GetProviderByIdAsync(state.ProviderId, ct);
        if (providerSettings == null || !providerFactory.TryGetProvider(providerSettings.ProviderId, out var provider))
            throw new Exception("Provider not found");

        var credentials =
            await provider.GetTokenAsync(providerSettings.Parameters, query, GetCallbackUrl(provider.Key), ct);
        var info = await provider.GetUserInfoAsync(providerSettings.Parameters, credentials, ct);
        info.AvatarUrl ??= imageService.CreateRandomAvatarUrl(info.Name ?? info.Login ?? "");

        var account = await accountRepository.GetAccountByProviderIdAsync(providerSettings.Id, info.Id, ct);
        Guid userId;
        if (account == null)
        {
            if (state.LinkUserId.HasValue)
                userId = state.LinkUserId.Value;
            else
                userId = await userRepository.CreateUserAsync(providerSettings.ApplicationId, ct);
            await accountRepository.CreateAccountAsync(userId, providerSettings.Id, info,
                providerSettings.Parameters.SaveTokens ? ProtectCredentials(credentials) : null,
                ct);
        }
        else
        {
            if (state.LinkUserId.HasValue && state.LinkUserId != account.UserId)
                throw new Exception("Account is already linked to another user");
            userId = account.UserId;
            if (!string.IsNullOrEmpty(account.TokenPair.RefreshToken))
                await provider.RevokeTokenAsync(providerSettings.Parameters, UnprotectCredentials(account.TokenPair),
                    ct);
            await accountRepository.UpdateAccountTokensAsync(account.Id,
                providerSettings.Parameters.SaveTokens ? ProtectCredentials(credentials) : new AccountCredentials(),
                ct);
            await accountRepository.UpdateAccountInfoAsync(account.Id, info, ct);
        }

        if (!providerSettings.Parameters.SaveTokens)
            await provider.RevokeTokenAsync(providerSettings.Parameters, credentials, ct);

        return new ProcessedAuthorizationState(userId, state);
    }

    public async Task<bool> CheckClientSecretAsync(string clientId, string clientSecret, CancellationToken ct = default)
    {
        var application = await applicationRepository.GetApplicationByClientIdAsync(clientId, ct);
        if (application == null)
            throw new Exception("Client not found");
        return application.ClientSecret == clientSecret;
    }

    private AccountCredentials ProtectCredentials(AccountCredentials unprotected)
    {
        return new AccountCredentials
        {
            AccessToken = unprotected.AccessToken is null ? null : secretProtector.Protect(unprotected.AccessToken),
            RefreshToken = unprotected.RefreshToken is null ? null : secretProtector.Protect(unprotected.RefreshToken),
            ExpiresAt = unprotected.ExpiresAt,
        };
    }

    private AccountCredentials UnprotectCredentials(AccountCredentials credentials)
    {
        return new AccountCredentials
        {
            AccessToken = credentials.AccessToken is null ? null : secretProtector.Unprotect(credentials.AccessToken),
            RefreshToken =
                credentials.RefreshToken is null ? null : secretProtector.Unprotect(credentials.RefreshToken),
            ExpiresAt = credentials.ExpiresAt,
        };
    }
}