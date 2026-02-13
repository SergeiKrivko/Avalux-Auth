using System.Security.Cryptography;
using System.Text.Json;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AvaluxAuth.Services;

public class SigningKeyService(
    ISigningKeyRepository signingKeyRepository,
    ISecretProtector secretProtector,
    IConfiguration configuration) : ISigningKeyService
{
    public async Task<SigningKey> GetActiveSigningKeyAsync(CancellationToken ct = default)
    {
        var key = await signingKeyRepository.GetActiveAsync(ct);
        if (key == null)
        {
            key = CreateSigningKey();
            await signingKeyRepository.AddAsync(key, ct);
        }

        return key;
    }

    public async Task<IEnumerable<SigningKey>> GetAllKeysAsync(CancellationToken ct = default)
    {
        return await signingKeyRepository.GetAllAsync(ct);
    }

    public async Task RotateSigningKeyAsync(CancellationToken ct = default)
    {
        var key = CreateSigningKey();
        await signingKeyRepository.AddAsync(key, ct);
    }

    private SigningKey CreateSigningKey()
    {
        using var rsa = RSA.Create(2048);
        var keyId = Guid.NewGuid().ToString("N");
        var privateKey = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());
        var protectedPrivateKey = secretProtector.Protect(privateKey);

        var parameters = rsa.ExportParameters(false);
        var jwk = new JsonWebKey
        {
            Kid = keyId,
            Kty = "RSA",
            Use = "sig",
            Alg = SecurityAlgorithms.RsaSha256,
            E = Base64UrlEncoder.Encode(parameters.Exponent),
            N = Base64UrlEncoder.Encode(parameters.Modulus)
        };

        var jwkJson = JsonSerializer.Serialize(jwk);

        return new SigningKey
        {
            Id = Guid.NewGuid(),
            Kid = keyId,
            Algorithm = SecurityAlgorithms.RsaSha256,
            Use = "sig",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(double.Parse(configuration["Security.SigningKeyRotationDays"] ?? "30")),
            IsActive = true,
            PrivateKeyEncrypted = protectedPrivateKey,
            PublicJwkJson = jwkJson
        };
    }
}