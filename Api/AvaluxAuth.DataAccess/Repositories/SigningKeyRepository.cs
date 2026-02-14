using AvaluxAuth.Abstractions;
using AvaluxAuth.DataAccess.Entities;
using AvaluxAuth.Models;
using Microsoft.EntityFrameworkCore;

namespace AvaluxAuth.DataAccess.Repositories;

public class SigningKeyRepository(AvaluxAuthDbContext dbContext) : ISigningKeyRepository
{
    public async Task<SigningKey?> GetActiveAsync(CancellationToken ct = default)
    {
        var entity = await dbContext.SigningKeys
            .Where(e => e.IsActive)
            .FirstOrDefaultAsync(ct);
        return entity is null ? null : FromEntity(entity);
    }

    public SigningKey? GetActive()
    {
        var entity = dbContext.SigningKeys
            .FirstOrDefault(e => e.IsActive);
        return entity is null ? null : FromEntity(entity);
    }

    public async Task<IEnumerable<SigningKey>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await dbContext.SigningKeys.ToListAsync(ct);
        return entities.Select(FromEntity);
    }

    public async Task AddAsync(SigningKey key, CancellationToken ct = default)
    {
        await dbContext.SigningKeys
            .Where(e => e.IsActive)
            .ExecuteUpdateAsync(e => e.SetProperty(x => x.IsActive, false), ct);
        await dbContext.SigningKeys.AddAsync(new SigningKeyEntity
        {
            Id = key.Id,
            Algorithm = key.Algorithm,
            Kid = key.Kid,
            Use = key.Use,
            PrivateKeyEncrypted = key.PrivateKeyEncrypted,
            PublicJwkJson = key.PublicJwkJson,
            IsActive = key.IsActive,
            CreatedAt = key.CreatedAt,
            ExpiresAt = key.ExpiresAt,
        }, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    private static SigningKey FromEntity(SigningKeyEntity entity)
    {
        return new SigningKey
        {
            Id = entity.Id,
            Algorithm = entity.Algorithm,
            Kid = entity.Kid,
            Use = entity.Use,
            PrivateKeyEncrypted = entity.PrivateKeyEncrypted,
            PublicJwkJson = entity.PublicJwkJson,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            ExpiresAt = entity.ExpiresAt,
        };
    }
}