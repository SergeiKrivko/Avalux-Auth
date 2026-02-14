using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface ITokenRepository
{
    public Task<Token?> GetTokenByIdAsync(Guid tokenId, CancellationToken ct = default);

    public Task<IEnumerable<Token>> GetTokensAsync(Guid applicationId, CancellationToken ct = default);

    public Task<Guid> CreateTokenAsync(Guid applicationId, string? name, string[] permissions, DateTime expiresAt,
        CancellationToken ct = default);

    public Task<bool> RemoveTokenAsync(Guid tokenId, CancellationToken ct = default);
}