using AvaluxAuth.Abstractions;
using Microsoft.Extensions.Logging;

namespace AvaluxAuth.Services;

public class UserService(
    IUserRepository userRepository,
    IProviderRepository providerRepository,
    IAccountRepository accountRepository,
    IEnumerable<IAuthProvider> authProviders,
    ILogger<UserService> logger) : IUserService
{
    public async Task<string?> GetAccessTokenAsync(Guid userId, string providerKey, CancellationToken ct)
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

        if (account.TokenPair.ExpiresAt - DateTimeOffset.UtcNow > TimeSpan.FromMinutes(1))
            return account.TokenPair.AccessToken;
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Access token for user {userId} has expired at {expiresAt}", userId,
                account.TokenPair.ExpiresAt);

        if (!providerData.Parameters.SaveTokens)
        {
            logger.LogWarning("No refresh tokens found for provider {provider}", provider.Name);
            return null;
        }

        if (account.TokenPair.RefreshToken is null)
            return null;

        var newCredentials =
            await provider.RefreshTokenAsync(providerData.Parameters, account.TokenPair.RefreshToken, ct);
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Token for user {userId} has refreshed successfully", userId);

        await accountRepository.UpdateAccountTokensAsync(account.Id, newCredentials, ct);
        return newCredentials.AccessToken;
    }

    public async Task<string?> GetAccessTokenAsync(Guid userId, Guid providerId, CancellationToken ct)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Selecting access token for user {userId}", userId);
        var account = await accountRepository.FindAccountAsync(userId, providerId, ct);
        if (account is null)
            return null;

        if (account.TokenPair.ExpiresAt - DateTimeOffset.UtcNow > TimeSpan.FromMinutes(1))
            return account.TokenPair.AccessToken;
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Access token for user {userId} has expired at {expiresAt}", userId,
                account.TokenPair.ExpiresAt);

        var providerData = await providerRepository.GetProviderByIdAsync(providerId, ct);
        if (providerData is null || account.TokenPair.RefreshToken is null)
            return null;
        var provider = authProviders.First(e => e.Id == providerData.ProviderId);
        if (!providerData.Parameters.SaveTokens)
        {
            logger.LogWarning("No refresh tokens found for provider {provider}", provider.Name);
            return null;
        }

        var newCredentials =
            await provider.RefreshTokenAsync(providerData.Parameters, account.TokenPair.RefreshToken, ct);
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Token for user {userId} has refreshed successfully", userId);

        await accountRepository.UpdateAccountTokensAsync(account.Id, newCredentials, ct);
        return newCredentials.AccessToken;
    }
}