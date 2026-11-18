namespace AvaluxAuth.Abstractions;

public interface IRefreshTokenRepository
{
    public Task AddRefreshTokenAsync(string refreshToken, Guid userId, string clientId, DateTime expiresAt,
        CancellationToken ct = default);
    public Task<bool> DeleteRefreshTokenAsync(string refreshToken, string clientId, CancellationToken ct = default);
    public Task<Guid?> ReplaceRefreshTokenAsync(string oldToken, string newToken, string clientId,
        CancellationToken ct = default);
}
