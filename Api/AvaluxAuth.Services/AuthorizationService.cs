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
    public async Task<string> CreateAuthorizationCodeAsync(Guid userId, CancellationToken ct = default)
    {
        var code = RandomNumberGenerator.GetRandomString();
        await authCodeRepository.SaveCodeAsync(new AuthCode
        {
            Code = code,
            UserId = userId,
        });
        return code;
    }

    public async Task<bool> RevokeTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        return await refreshTokenRepository.DeleteRefreshTokenAsync(refreshToken, ct);
    }

    public async Task<UserCredentials> GetTokenAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepository.GetUserWithSubscriptionsAsync(userId, ct);
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

    public async Task<UserCredentials> GetTokenAsync(string code, CancellationToken ct = default)
    {
        var codeData = await authCodeRepository.TakeCodeAsync(code);
        if (codeData == null)
            throw new Exception("Invalid auth code");
        return await GetTokenAsync(codeData.UserId, ct);
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


    private async Task<string> CreateJwt(UserWithSubscriptions user, Application application, DateTime expiresAt,
        CancellationToken ct = default)
    {
        var jwt = new JwtSecurityToken(
            issuer: configuration["Security.Issuer"],
            audience: application.Parameters.Name,
            claims:
            [
                new Claim("UserId", user.Id.ToString()),
                new Claim("Subscriptions", string.Join(';', user.Subscriptions.Select(e => e.Plan)))
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
        var accessToken = await CreateJwt(user, application ?? throw new Exception("Application not found"), expiresAt,
            ct);
        return new UserCredentials
        {
            AccessToken = accessToken,
            RefreshToken = newToken,
            ExpiresAt = expiresAt,
        };
    }
}