namespace AvaluxAuth.Abstractions;

public interface IRefreshTokenRepository
{
    public Task AddRefreshTokenAsync(string refreshToken, Guid userId, CancellationToken ct = default);
    public Task<bool> DeleteRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    public Task<Guid?> ReplaceRefreshTokenAsync(string oldToken, string newToken, CancellationToken ct = default);
}