using AvaluxAuth.Abstractions;
using AvaluxAuth.DataAccess.Entities;
using AvaluxAuth.Models;
using Microsoft.EntityFrameworkCore;

namespace AvaluxAuth.DataAccess.Repositories;

public class ApplicationRepository(AvaluxAuthDbContext dbContext) : IApplicationRepository
{
    public async Task<Application?> GetApplicationByIdAsync(Guid applicationId, CancellationToken ct = default)
    {
        var res = await dbContext.Applications
            .Where(a => a.Id == applicationId && a.DeletedAt == null)
            .FirstOrDefaultAsync(ct);
        return res is null ? null : FromEntity(res);
    }

    public async Task<Application?> GetApplicationByClientIdAsync(string clientId, CancellationToken ct = default)
    {
        var res = await dbContext.Applications
            .Where(a => a.ClientId == clientId && a.DeletedAt == null)
            .FirstOrDefaultAsync(ct);
        return res is null ? null : FromEntity(res);
    }

    public async Task<IEnumerable<Application>> GetAllApplicationsAsync(CancellationToken ct = default)
    {
        var res = await dbContext.Applications
            .Where(a => a.DeletedAt == null)
            .ToListAsync(ct);
        return res.Select(FromEntity);
    }

    public async Task<Guid> CreateApplicationAsync(ApplicationParameters parameters, string clientId,
        string clientSecret, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var entity = new ApplicationEntity
        {
            Id = id,
            ClientId = clientId,
            ClientSecret = clientSecret,
            Name = parameters.Name,
            RedirectUrls = parameters.RedirectUrls,
            CreatedAt = DateTime.UtcNow,
            DeletedAt = null,
        };
        await dbContext.Applications.AddAsync(entity, ct);
        await dbContext.SaveChangesAsync(ct);
        return id;
    }

    public async Task<bool> UpdateApplicationAsync(Guid applicationId, ApplicationParameters parameters,
        CancellationToken ct = default)
    {
        var count = await dbContext.Applications
            .Where(a => a.Id == applicationId && a.DeletedAt == null)
            .ExecuteUpdateAsync(a =>
            {
                a.SetProperty(x => x.Name, parameters.Name);
                a.SetProperty(x => x.RedirectUrls, parameters.RedirectUrls);
            }, ct);
        await dbContext.SaveChangesAsync(ct);
        return count > 0;
    }

    public async Task<bool> UpdateClientSecretAsync(Guid applicationId, string clientSecret, CancellationToken ct = default)
    {
        var count = await dbContext.Applications
            .Where(a => a.Id == applicationId && a.DeletedAt == null)
            .ExecuteUpdateAsync(a =>
            {
                a.SetProperty(x => x.ClientSecret, clientSecret);
            }, ct);
        await dbContext.SaveChangesAsync(ct);
        return count > 0;
    }

    public async Task<bool> DeleteApplicationAsync(Guid applicationId, CancellationToken ct = default)
    {
        var count = await dbContext.Applications
            .Where(a => a.Id == applicationId && a.DeletedAt == null)
            .ExecuteUpdateAsync(a =>
            {
                a.SetProperty(x => x.DeletedAt, DateTime.UtcNow);
            }, ct);
        await dbContext.SaveChangesAsync(ct);
        return count > 0;
    }

    private static Application FromEntity(ApplicationEntity entity)
    {
        return new Application
        {
            Id = entity.Id,
            ClientId = entity.ClientId,
            ClientSecret = entity.ClientSecret,
            Parameters = new ApplicationParameters
            {
                Name = entity.Name,
                RedirectUrls = entity.RedirectUrls,
            },
            CreatedAt = entity.CreatedAt,
            DeletedAt = entity.DeletedAt,
        };
    }
}