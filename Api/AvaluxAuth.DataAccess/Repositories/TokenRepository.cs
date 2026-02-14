using AvaluxAuth.Abstractions;
using AvaluxAuth.DataAccess.Entities;
using AvaluxAuth.Models;
using Microsoft.EntityFrameworkCore;

namespace AvaluxAuth.DataAccess.Repositories;

public class TokenRepository(AvaluxAuthDbContext dbContext) : ITokenRepository
{
    public async Task<Token?> GetTokenByIdAsync(Guid tokenId, CancellationToken ct = default)
    {
        var entity = await dbContext.Tokens
            .Where(x => x.Id == tokenId && x.DeletedAt != null)
            .FirstOrDefaultAsync(ct);
        return entity is null ? null : FromEntity(entity);
    }

    public async Task<IEnumerable<Token>> GetTokensAsync(Guid applicationId, CancellationToken ct = default)
    {
        var entities = await dbContext.Tokens
            .Where(x => x.ApplicationId == applicationId && x.DeletedAt != null)
            .ToListAsync(ct);
        return entities.Select(FromEntity);
    }

    public async Task<Guid> CreateTokenAsync(Guid applicationId, string? name, string[] permissions, DateTime expiresAt,
        CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var entity = new TokenEntity
        {
            Id = id,
            ApplicationId = applicationId,
            Name = name,
            Permissions = permissions,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
        };
        await dbContext.Tokens.AddAsync(entity, ct);
        await dbContext.SaveChangesAsync(ct);
        return id;
    }

    public async Task<bool> RemoveTokenAsync(Guid tokenId, CancellationToken ct = default)
    {
        var count = await dbContext.Tokens
            .Where(x => x.Id == tokenId && x.DeletedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(e => e.DeletedAt, DateTime.UtcNow), ct);
        await dbContext.SaveChangesAsync(ct);
        return count > 0;
    }

    private static Token FromEntity(TokenEntity entity)
    {
        return new Token
        {
            Id = entity.Id,
            ApplicationId = entity.ApplicationId,
            Name = entity.Name,
            Permissions = entity.Permissions,
            CreatedAt = entity.CreatedAt,
            ExpiresAt = entity.ExpiresAt,
            DeletedAt = entity.DeletedAt,
        };
    }
}