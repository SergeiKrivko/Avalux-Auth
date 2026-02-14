using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AvaluxAuth.Services;

public class TokenService(
    ITokenRepository tokenRepository,
    ISigningKeyService signingKeyService,
    IConfiguration configuration,
    ISecretProtector secretProtector) : ITokenService
{
    public async Task<string> CreateTokenAsync(Guid applicationId, string? name, string[] permissions,
        DateTime expiresAt,
        CancellationToken ct = default)
    {
        if (permissions.ContainsAnyExcept(TokenPermissions.All))
            throw new ArgumentException("Permissions are not allowed");
        var id = await tokenRepository.CreateTokenAsync(applicationId, name, permissions, expiresAt, ct);
        var jwt = await CreateJwt(id, applicationId, expiresAt, permissions, ct);
        return jwt;
    }

    public async Task<bool> VerifyPermissionAsync(Guid tokenId, string permission, CancellationToken ct = default)
    {
        var token = await tokenRepository.GetTokenByIdAsync(tokenId, ct);
        return token?.Permissions.Contains(permission) ?? false;
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