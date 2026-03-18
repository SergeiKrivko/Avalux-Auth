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
    public async Task<string> CreateAuthorizationCodeAsync(Guid userId, string? nonce, CancellationToken ct = default)
    {
        var code = RandomNumberGenerator.GetRandomString();
        await authCodeRepository.SaveCodeAsync(new AuthCode
        {
            Code = code,
            UserId = userId,
            UserNonce = nonce,
            AuthTime = DateTimeOffset.UtcNow,
        });
        return code;
    }

    public async Task<bool> RevokeTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        return await refreshTokenRepository.DeleteRefreshTokenAsync(refreshToken, ct);
    }

    private async Task<UserCredentials> GetTokenAsync(Guid userId, AuthCode codeData, CancellationToken ct = default)
    {
        var user = await userRepository.GetUserWithSubscriptionsAsync(userId, ct);
        if (user == null)
            throw new Exception("User not found");
        var application = await applicationRepository.GetApplicationByIdAsync(user.ApplicationId, ct);
        if (application == null)
            throw new Exception("Application not found");

        var expiresAt = DateTime.UtcNow.AddHours(1);
        var accessToken = await CreateAccessToken(user, application, expiresAt, ct);
        var idToken = await CreateIdToken(user, application, expiresAt, codeData.UserNonce, codeData.AuthTime, ct);

        var refreshToken = RandomNumberGenerator.GetRandomString(128);
        await refreshTokenRepository.AddRefreshTokenAsync(refreshToken, user.Id, ct);

        return new UserCredentials
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            IdToken = idToken,
        };
    }

    public async Task<UserCredentials> GetTokenAsync(string code, CancellationToken ct = default)
    {
        var codeData = await authCodeRepository.TakeCodeAsync(code);
        if (codeData == null)
            throw new Exception("Invalid auth code");
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
            new("Subscriptions", string.Join(';', user.Subscriptions.Select(e => e.Plan)))
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
            issuer: configuration["Security.Issuer"],
            audience: application.ClientId,
            claims: claims,
            expires: expiresAt,
            signingCredentials: await GetSecurityKeyAsync(ct)
        );
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    public async Task<UserCredentials?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var newToken = RandomNumberGenerator.GetRandomString(128);
        var userId = await refreshTokenRepository.ReplaceRefreshTokenAsync(refreshToken, newToken, ct);
        if (userId is null)
        {
            logger.LogWarning("Refresh token not found");
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
}