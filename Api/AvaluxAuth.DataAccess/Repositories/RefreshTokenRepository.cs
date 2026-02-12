using AvaluxAuth.Abstractions;
using AvaluxAuth.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AvaluxAuth.DataAccess.Repositories;

public class RefreshTokenRepository(AvaluxAuthDbContext dbContext) : IRefreshTokenRepository
{
    public async Task AddRefreshTokenAsync(string refreshToken, Guid userId, CancellationToken ct = default)
    {
        await dbContext.RefreshTokens.AddAsync(new RefreshTokenEntity
        {
            RefreshToken = refreshToken,
            UserId = userId,
        }, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        await dbContext.RefreshTokens
            .Where(e => e.RefreshToken == refreshToken)
            .ExecuteDeleteAsync(ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<Guid?> ReplaceRefreshTokenAsync(string oldToken, string newToken, CancellationToken ct = default)
    {
        var token = await dbContext.RefreshTokens
            .Where(e => e.RefreshToken == oldToken)
            .FirstOrDefaultAsync(ct);
        if (token == null)
            return null;
        await dbContext.RefreshTokens
            .Where(e => e.RefreshToken == oldToken)
            .ExecuteUpdateAsync(e => e.SetProperty(x => x.RefreshToken, newToken), ct);
        await dbContext.SaveChangesAsync(ct);
        return token.UserId;
    }
}