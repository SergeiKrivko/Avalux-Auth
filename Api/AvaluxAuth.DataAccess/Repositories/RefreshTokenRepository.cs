using AvaluxAuth.Abstractions;
using AvaluxAuth.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AvaluxAuth.DataAccess.Repositories;

public class RefreshTokenRepository(AvaluxAuthDbContext dbContext) : IRefreshTokenRepository
{
    public async Task AddRefreshTokenAsync(string refreshToken, Guid userId, string clientId, DateTime expiresAt,
        CancellationToken ct = default)
    {
        await dbContext.RefreshTokens.AddAsync(new RefreshTokenEntity
        {
            RefreshToken = refreshToken,
            UserId = userId,
            ClientId = clientId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
        }, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteRefreshTokenAsync(string refreshToken, string clientId, CancellationToken ct = default)
    {
        // A client may only revoke its own tokens (H-05).
        var count = await dbContext.RefreshTokens
            .Where(e => e.RefreshToken == refreshToken && e.ClientId == clientId)
            .ExecuteDeleteAsync(ct);
        return count > 0;
    }

    public async Task<Guid?> ReplaceRefreshTokenAsync(string oldToken, string newToken, string clientId,
        CancellationToken ct = default)
    {
        var token = await dbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.RefreshToken == oldToken, ct);
        if (token == null || token.ClientId != clientId)
            return null;

        if (token.ExpiresAt <= DateTime.UtcNow)
        {
            await dbContext.RefreshTokens
                .Where(e => e.RefreshToken == oldToken)
                .ExecuteDeleteAsync(ct);
            return null;
        }

        // Rotation keeps ClientId/CreatedAt/ExpiresAt: the absolute lifetime is not extended.
        var count = await dbContext.RefreshTokens
            .Where(e => e.RefreshToken == oldToken && e.ClientId == clientId && e.ExpiresAt > DateTime.UtcNow)
            .ExecuteUpdateAsync(e => e.SetProperty(x => x.RefreshToken, newToken), ct);
        return count > 0 ? token.UserId : null;
    }
}
