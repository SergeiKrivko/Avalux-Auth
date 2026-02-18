using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace AvaluxAuth.Services;

public class TokenService(
    ITokenRepository tokenRepository,
    ISigningKeyService signingKeyService,
    IConfiguration configuration,
    ISecretProtector secretProtector,
    IConnectionMultiplexer redis) : ITokenService
{
    private const string RedisKey = "ServiceAccuntTokens";

    public async Task<(Guid, string)> CreateTokenAsync(Guid applicationId, string? name, string[] permissions,
        DateTime expiresAt,
        CancellationToken ct = default)
    {
        if (permissions.ContainsAnyExcept(TokenPermission.AllKeys))
            throw new ArgumentException("Permissions are not allowed");
        var id = await tokenRepository.CreateTokenAsync(applicationId, name, permissions, expiresAt, ct);
        var jwt = await CreateJwt(id, applicationId, expiresAt, permissions, ct);
        return (id, jwt);
    }

    public async Task<bool> VerifyPermissionAsync(Guid tokenId, string permission, CancellationToken ct = default)
    {
        var token = await tokenRepository.GetTokenByIdAsync(tokenId, ct);
        return token?.Permissions.Contains(permission) ?? false;
    }

    public async Task<bool> IsRevokedAsync(Guid tokenId, CancellationToken ct = default)
    {
        var redisDb = redis.GetDatabase();
        if (await redisDb.SetContainsAsync(RedisKey, tokenId.ToString()))
            return true;

        var token = await tokenRepository.GetTokenByIdAsync(tokenId, ct);
        if (token == null)
            return false;
        await redisDb.SetAddAsync(RedisKey, tokenId.ToString());
        return true;
    }

    public async Task<bool> RevokeTokenAsync(Guid tokenId, CancellationToken ct = default)
    {
        var res = await tokenRepository.RemoveTokenAsync(tokenId, ct);
        if (res)
        {
            var redisDb = redis.GetDatabase();
            await redisDb.SetRemoveAsync(RedisKey, tokenId.ToString());
        }

        return res;
    }

    private const string ServiceAccountAudience = "AvaluxAuthServiceAccount";

    private async Task<string> CreateJwt(Guid id, Guid applicationId, DateTime expiresAt, string[] permissions,
        CancellationToken ct = default)
    {
        var jwt = new JwtSecurityToken(
            issuer: configuration["Security.Issuer"],
            audience: ServiceAccountAudience,
            claims:
            [
                new Claim(ClaimTypes.Role, "ServiceAccount"),
                new Claim("TokenId", id.ToString()),
                new Claim("ApplicationId", applicationId.ToString()),
                new Claim("Permissions", string.Join(';', permissions)),
            ],
            expires: expiresAt,
            signingCredentials: await GetSecurityKeyAsync(ct)
        );
        return new JwtSecurityTokenHandler().WriteToken(jwt);
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
}