using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using AvaluxAuth.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace AvaluxAuth.Services;

public class AuthorizationService(
    IApplicationRepository applicationRepository,
    IAuthCodeRepository authCodeRepository,
    IConfiguration configuration,
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ISigningKeyService signingKeyService,
    ISecretProtector secretProtector,
    ILogger<OauthService> logger) : IAuthorizationService
{
    private const int DefaultRefreshTokenLifetimeDays = 30;

    public async Task<string> CreateAuthorizationCodeAsync(Guid userId, string clientId, string redirectUri,
        string? nonce, string? codeChallenge, string? codeChallengeMethod, CancellationToken ct = default)
    {
        var code = RandomNumberGenerator.GetRandomString();
        await authCodeRepository.SaveCodeAsync(new AuthCode
        {
            Code = code,
            UserId = userId,
            UserNonce = nonce,
            AuthTime = DateTimeOffset.UtcNow,
            // Binding the code to the client, redirect URI and PKCE challenge means an
            // intercepted code is useless without the matching verifier (H-05).
            ClientId = clientId,
            RedirectUri = redirectUri,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = codeChallengeMethod,
            // M-03: a code cannot outlive the credential it was issued for. Codes minted
            // during a linking flow are created after that flow's revocation, so they
            // carry the new generation and still work.
            SessionVersion = await userRepository.GetSessionVersionAsync(userId, ct) ?? 0,
        });
        return code;
    }

    public async Task<bool> RevokeTokenAsync(string refreshToken, string clientId, CancellationToken ct = default)
    {
        return await refreshTokenRepository.DeleteRefreshTokenAsync(refreshToken, clientId, ct);
    }

    private async Task<UserCredentials?> GetTokenAsync(Guid userId, AuthCode codeData, CancellationToken ct = default)
    {
        var user = await userRepository.GetUserWithSubscriptionsAsync(userId, ct);
        if (user == null)
        {
            // The code was issued for a user that has been deleted since. That is a stale
            // grant, not a server fault: the caller must see invalid_grant (400) instead of
            // an unhandled 500.
            logger.LogWarning("Authorization code refers to a user that no longer exists");
            return null;
        }

        // M-03: the code was issued before the user's credentials changed, so it must
        // not turn into a token that would carry the new session generation.
        if (codeData.SessionVersion != user.SessionVersion)
        {
            logger.LogWarning("Authorization code was issued before the user's sessions were revoked");
            return null;
        }

        var application = await applicationRepository.GetApplicationByIdAsync(user.ApplicationId, ct);
        if (application == null)
            throw new Exception("Application not found");
        if (!string.Equals(application.ClientId, codeData.ClientId, StringComparison.Ordinal))
        {
            logger.LogWarning("Authorization code client does not match the user application");
            return null;
        }

        var expiresAt = DateTime.UtcNow.AddHours(1);
        var accessToken = await CreateAccessToken(user, application, expiresAt, ct);
        var idToken = await CreateIdToken(user, application, expiresAt, codeData.UserNonce, codeData.AuthTime, ct);

        var refreshToken = RandomNumberGenerator.GetRandomString(128);
        await refreshTokenRepository.AddRefreshTokenAsync(refreshToken, user.Id, application.ClientId,
            GetRefreshTokenExpiresAt(), ct);

        return new UserCredentials
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            IdToken = idToken,
        };
    }

    public async Task<UserCredentials?> GetTokenAsync(string code, string clientId, string? codeVerifier,
        string? redirectUri, CancellationToken ct = default)
    {
        // The code is consumed on every attempt: a failed PKCE check must not allow retries.
        var codeData = await authCodeRepository.TakeCodeAsync(code);
        if (codeData == null)
        {
            logger.LogWarning("Authorization code not found");
            return null;
        }

        if (string.IsNullOrEmpty(codeData.ClientId) ||
            !string.Equals(codeData.ClientId, clientId, StringComparison.Ordinal))
        {
            logger.LogWarning("Authorization code was issued to another client");
            return null;
        }

        if (!string.Equals(codeData.RedirectUri, redirectUri, StringComparison.Ordinal))
        {
            logger.LogWarning("redirect_uri does not match the authorization request");
            return null;
        }

        if (!string.IsNullOrEmpty(codeData.CodeChallenge))
        {
            if (!Pkce.Verify(codeVerifier, codeData.CodeChallenge, codeData.CodeChallengeMethod))
            {
                logger.LogWarning("PKCE verification failed");
                return null;
            }
        }
        else
        {
            // Public clients always get a challenge at /authorize. A code without one is
            // only acceptable for an authenticated confidential client.
            var client = await applicationRepository.GetApplicationByClientIdAsync(codeData.ClientId, ct);
            if (client == null || !client.Parameters.RequireClientSecret)
            {
                logger.LogWarning("Authorization code without PKCE challenge for a public client");
                return null;
            }
        }

        return await GetTokenAsync(codeData.UserId, codeData, ct);
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


    private async Task<string> CreateAccessToken(UserWithSubscriptions user, Application application, DateTime expiresAt,
        CancellationToken ct = default)
    {
        List<Claim> claims =
        [
            new("UserId", user.Id.ToString()),
            new("Subscriptions", string.Join(';', user.Subscriptions.Select(e => e.Plan))),
            // M-03: the token is only valid while this is the user's current session
            // version. Changing the password or linking a credential bumps it.
            new("SessionVersion", user.SessionVersion.ToString(CultureInfo.InvariantCulture)),
        ];
        return await CreateJwt(application, claims, expiresAt, ct);
    }


    private async Task<string> CreateIdToken(UserWithSubscriptions user, Application application, DateTime expiresAt,
        string? nonce, DateTimeOffset authTime,
        CancellationToken ct = default)
    {
        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.AuthTime, authTime.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
        ];
        if (nonce != null)
            claims.Add(new Claim(JwtRegisteredClaimNames.Nonce, nonce));
        return await CreateJwt(application, claims, expiresAt, ct);
    }


    private async Task<string> CreateJwt(Application application, IEnumerable<Claim> claims, DateTime expiresAt,
        CancellationToken ct = default)
    {
        var jwt = new JwtSecurityToken(
            issuer: configuration["Security:Issuer"],
            audience: application.ClientId,
            claims: claims,
            expires: expiresAt,
            signingCredentials: await GetSecurityKeyAsync(ct)
        );
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    public async Task<UserCredentials?> RefreshTokenAsync(string refreshToken, string clientId,
        CancellationToken ct = default)
    {
        var newToken = RandomNumberGenerator.GetRandomString(128);
        var userId = await refreshTokenRepository.ReplaceRefreshTokenAsync(refreshToken, newToken, clientId, ct);
        if (userId is null)
        {
            logger.LogWarning("Refresh token not found, expired or issued to another client");
            return null;
        }

        var user = await userRepository.GetUserWithSubscriptionsAsync(userId.Value, ct);
        if (user is null)
        {
            logger.LogWarning("User from refresh token not found");
            return null;
        }

        var application = await applicationRepository.GetApplicationByIdAsync(user.ApplicationId, ct);
        var expiresAt = DateTime.UtcNow.AddHours(1);
        var accessToken = await CreateAccessToken(user, application ?? throw new Exception("Application not found"), expiresAt, ct);
        return new UserCredentials
        {
            AccessToken = accessToken,
            RefreshToken = newToken,
            ExpiresAt = expiresAt,
        };
    }

    private DateTime GetRefreshTokenExpiresAt()
    {
        var days = int.TryParse(configuration["Security:RefreshTokenLifetimeDays"], out var configured) && configured > 0
            ? configured
            : DefaultRefreshTokenLifetimeDays;
        return DateTime.UtcNow.AddDays(days);
    }
}
