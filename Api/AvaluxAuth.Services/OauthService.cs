using System.Security.Cryptography;
using System.Text;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using AvaluxAuth.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
    IImageService imageService,
    ILogger<OauthService> logger) : IOauthService
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
        string? userState, string? userNonce, string? linkCode, string? codeChallenge, string? codeChallengeMethod,
        CancellationToken ct = default)
    {
        var application = await applicationRepository.GetApplicationByClientIdAsync(clientId, ct);
        if (application == null)
            throw new Exception("Client not found");

        if (string.IsNullOrEmpty(redirectUri) || !application.Parameters.RedirectUrls.Contains(redirectUri))
            throw new Exception("Invalid redirect url");

        // RFC 6749 §3.1.2: the redirection endpoint URI must not include a fragment.
        if (redirectUri.Contains('#'))
            throw new Exception("Invalid redirect url");

        ValidatePkce(application, codeChallenge, codeChallengeMethod);

        if (!providerFactory.TryGetProvider(providerKey, out var provider))
            throw new Exception("Provider not found");
        var providerSettings = await providerRepository.GetProviderByProviderIdAsync(application.Id, provider.Id, ct);
        if (providerSettings == null)
            throw new Exception("Provider is not added to this application");

        var link = linkCode == null ? null : await linkCodeRepository.TakeCodeAsync(linkCode);
        var existingAccount = link == null
            ? null
            : (await accountRepository.GetAccountsOfUserAsync(link.UserId, ct)).FirstOrDefault(e =>
                e.ProviderId == providerSettings.Id);

        // M-05: diagnostics only — no link code, user id or account identity in the log.
        logger.LogDebug(
            "Authorization request resolved: linkCodePresent={LinkCodePresent}, accountAlreadyLinked={AccountAlreadyLinked}",
            link != null, existingAccount != null);
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
            ClientId = application.ClientId,
            ProviderId = providerSettings.Id,
            RedirectUrl = redirectUri,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = codeChallengeMethod,
        });

        if (existingAccount != null)
            return $"{configuration["Api:ApiUrl"]}/profile?state={state}";

        return provider.Id == 0
            ? $"{configuration["Api:ApiUrl"]}/login?state={state}"
            : provider.GetAuthUrl(providerSettings.Parameters, GetCallbackUrl(provider.Key), state);
    }

    private string GetCallbackUrl(string providerKey)
    {
        return
            $"{configuration["Api:ApiUrl"] ?? throw new Exception("Api url not found")}/api/v1/auth/{providerKey}/callback";
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
        if (account != null && await userRepository.GetUserAsync(account.UserId, ct) is null)
        {
            // The account outlived its user: deleting a user used to leave the linked
            // accounts behind, and such a stale account would send us to a user that no
            // longer exists — /token then answered 500 instead of signing the person in.
            // Drop the orphan so this sign-in starts from a fresh user.
            logger.LogWarning("Discarding an account whose user was deleted: provider={Provider}",
                providerSettings.ProviderId);
            await accountRepository.DeleteAccountAsync(account.Id, ct);
            account = null;
        }

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

            // M-03: a newly linked credential revokes the user's existing sessions.
            // The link flow mints new tokens for the user right afterwards.
            if (state.LinkUserId.HasValue)
            {
                var revoked = await userRepository.RevokeSessionsAsync(userId, ct);
                logger.LogWarning(
                    "All sessions revoked after linking an account: userId={UserId}, provider={Provider}, revoked={Revoked}",
                    userId, providerSettings.ProviderId, revoked);
            }
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

    /// <summary>
    /// Public clients (e.g. desktop apps with a loopback redirect URI) cannot keep a secret:
    /// they authenticate with PKCE instead. Confidential clients must present their exact
    /// client secret; the comparison is constant-time.
    /// </summary>
    public async Task<bool> AuthenticateClientAsync(string clientId, string? clientSecret,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return false;

        var application = await applicationRepository.GetApplicationByClientIdAsync(clientId, ct);
        if (application == null)
            return false;

        if (!application.Parameters.RequireClientSecret)
            return true;

        return SecretEquals(application.ClientSecret, clientSecret);
    }

    private static void ValidatePkce(Application application, string? codeChallenge, string? codeChallengeMethod)
    {
        if (string.IsNullOrEmpty(codeChallenge))
        {
            if (codeChallengeMethod != null)
                throw new Exception("code_challenge_method without code_challenge");

            // A public client is protected by PKCE only; a confidential client is also
            // authenticated with its secret at the token endpoint.
            if (!application.Parameters.RequireClientSecret)
                throw new Exception("PKCE code_challenge is required for this client");
            return;
        }

        if (!string.Equals(codeChallengeMethod, Pkce.S256, StringComparison.Ordinal))
            throw new Exception("Only PKCE S256 is supported");
        if (!Pkce.IsValidChallenge(codeChallenge))
            throw new Exception("Invalid code_challenge");
    }

    private static bool SecretEquals(string expected, string? actual)
    {
        if (string.IsNullOrEmpty(actual))
            return false;
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
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