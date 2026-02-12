using AvaluxAuth.Abstractions;
using AvaluxAuth.DataAccess.Entities;
using AvaluxAuth.Models;
using Microsoft.EntityFrameworkCore;

namespace AvaluxAuth.DataAccess.Repositories;

public class ProviderRepository(AvaluxAuthDbContext dbContext) : IProviderRepository
{
    public async Task<Provider?> GetProviderByIdAsync(Guid id, CancellationToken ct = default)
    {
        var res = await dbContext.Providers
            .Where(x => x.Id == id && x.DeletedAt == null)
            .FirstOrDefaultAsync(ct);
        return res is null ? null : FromEntity(res);
    }

    public async Task<Provider?> GetProviderByProviderIdAsync(Guid applicationId, int id, CancellationToken ct = default)
    {
        var res = await dbContext.Providers
            .Where(x => x.ApplicationId == applicationId && x.ProviderId == id && x.DeletedAt == null)
            .FirstOrDefaultAsync(ct);
        return res is null ? null : FromEntity(res);
    }

    public async Task<IEnumerable<Provider>> GetAllProvidersAsync(Guid applicationId, CancellationToken ct = default)
    {
        var res = await dbContext.Providers
            .Where(x => x.ApplicationId == applicationId && x.DeletedAt == null)
            .ToListAsync(ct);
        return res.Select(FromEntity);
    }

    public async Task<Guid> CreateProviderAsync(Guid applicationId, int providerId, ProviderParameters provider,
        CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var entity = new ProviderEntity
        {
            Id = id,
            ApplicationId = applicationId,
            ProviderId = providerId,

            ClientId = provider.ClientId,
            SaveTokens = provider.SaveTokens,

            CreatedAt = DateTime.UtcNow,
            DeletedAt = null,
        };
        await dbContext.Providers.AddAsync(entity, ct);
        await dbContext.SaveChangesAsync(ct);
        return id;
    }

    public async Task<bool> UpdateProviderAsync(Guid id, ProviderParameters provider, CancellationToken ct = default)
    {
        var count = await dbContext.Providers
            .Where(x => x.Id == id && x.DeletedAt == null)
            .ExecuteUpdateAsync(x =>
            {
                x.SetProperty(e => e.ClientId, provider.ClientId);
                x.SetProperty(e => e.ClientSecret, provider.ClientSecret);
                x.SetProperty(e => e.SaveTokens, provider.SaveTokens);
            }, ct);
        await dbContext.SaveChangesAsync(ct);
        return count > 0;
    }

    public async Task<bool> DeleteProviderAsync(Guid id, CancellationToken ct = default)
    {
        var count = await dbContext.Providers
            .Where(x => x.Id == id && x.DeletedAt == null)
            .ExecuteUpdateAsync(x =>
            {
                x.SetProperty(e => e.DeletedAt, DateTime.UtcNow);
            }, ct);
        await dbContext.SaveChangesAsync(ct);
        return count > 0;
    }

    private static Provider FromEntity(ProviderEntity entity)
    {
        return new Provider
        {
            Id = entity.Id,
            ApplicationId = entity.ApplicationId,
            ProviderId = entity.ProviderId,
            Parameters = new ProviderParameters
            {
                ClientId = entity.ClientId,
                ClientSecret = entity.ClientSecret,
                SaveTokens = entity.SaveTokens,
            },
            CreatedAt = entity.CreatedAt,
            DeletedAt = entity.DeletedAt,
        };
    }
}