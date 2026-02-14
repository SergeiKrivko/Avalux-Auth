namespace AvaluxAuth.Abstractions;

public interface ITokenService
{
    public Task<string> CreateTokenAsync(Guid applicationId, string? name, string[] permissions, DateTime expiresAt,
        CancellationToken ct = default);
    public Task<bool> VerifyPermissionAsync(Guid tokenId, string permission, CancellationToken ct = default);
}