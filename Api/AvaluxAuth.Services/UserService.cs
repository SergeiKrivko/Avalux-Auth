using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using Microsoft.Extensions.Logging;

namespace AvaluxAuth.Services;

public class UserService(
    IUserRepository userRepository,
    IProviderRepository providerRepository,
    IAccountRepository accountRepository,
    IEnumerable<IAuthProvider> authProviders,
    ILogger<UserService> logger,
    ISecretProtector secretProtector) : IUserService
{
    public async Task<AccountCredentials?> GetAccessTokenAsync(Guid userId, string providerKey, CancellationToken ct)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Selecting access token for user {userId}", userId);
        var provider = authProviders.First(e => e.Key == providerKey);
        var user = await userRepository.GetUserAsync(userId, ct);
        if (user is null)
            return null;
        var providerData = await providerRepository.GetProviderByProviderIdAsync(user.ApplicationId, provider.Id, ct);
        if (providerData is null)
            return null;

        var account = await accountRepository.FindAccountAsync(userId, providerData.Id, ct);
        if (account is null)
            return null;
        var credentials = UnprotectCredentials(account.TokenPair);

        if (credentials.ExpiresAt - DateTimeOffset.UtcNow > TimeSpan.FromMinutes(1))
            return WithNoRefreshToken(credentials);
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Access token for user {userId} has expired at {expiresAt}", userId,
                credentials.ExpiresAt);

        if (!providerData.Parameters.SaveTokens)
        {
            logger.LogWarning("No refresh tokens found for provider {provider}", provider.Name);
            return null;
        }

        if (credentials.RefreshToken is null)
            return null;

        var newCredentials =
            await provider.RefreshTokenAsync(providerData.Parameters, credentials.RefreshToken, ct);
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Token for user {userId} has refreshed successfully", userId);

        await accountRepository.UpdateAccountTokensAsync(account.Id, ProtectCredentials(newCredentials), ct);
        return WithNoRefreshToken(newCredentials);
    }

    public async Task<AccountCredentials?> GetAccessTokenAsync(Guid userId, Guid providerId, CancellationToken ct)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Selecting access token for user {userId}", userId);
        var account = await accountRepository.FindAccountAsync(userId, providerId, ct);
        if (account is null)
            return null;
        var credentials = UnprotectCredentials(account.TokenPair);

        if (credentials.ExpiresAt - DateTimeOffset.UtcNow > TimeSpan.FromMinutes(1))
            return WithNoRefreshToken(credentials);
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Access token for user {userId} has expired at {expiresAt}", userId,
                credentials.ExpiresAt);

        var providerData = await providerRepository.GetProviderByIdAsync(providerId, ct);
        if (providerData is null || credentials.RefreshToken is null)
            return null;
        var provider = authProviders.First(e => e.Id == providerData.ProviderId);
        if (!providerData.Parameters.SaveTokens)
        {
            logger.LogWarning("No refresh tokens found for provider {provider}", provider.Name);
            return null;
        }

        var newCredentials =
            await provider.RefreshTokenAsync(providerData.Parameters, credentials.RefreshToken, ct);
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Token for user {userId} has refreshed successfully", userId);

        await accountRepository.UpdateAccountTokensAsync(account.Id, ProtectCredentials(newCredentials), ct);
        return WithNoRefreshToken(newCredentials);
    }

    private static AccountCredentials WithNoRefreshToken(AccountCredentials credentials)
    {
        return new AccountCredentials
        {
            AccessToken = credentials.AccessToken,
            ExpiresAt = credentials.ExpiresAt,
            RefreshToken = null,
        };
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

    private AccountCredentials UnprotectCredentials(AccountCredentials source)
    {
        return new AccountCredentials
        {
            AccessToken = source.AccessToken is null ? null : secretProtector.Unprotect(source.AccessToken),
            RefreshToken = source.RefreshToken is null ? null : secretProtector.Unprotect(source.RefreshToken),
            ExpiresAt = source.ExpiresAt,
        };
    }
}