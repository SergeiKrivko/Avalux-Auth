using System.Text.Json;
using AvaluxAuth.Abstractions;
using AvaluxAuth.DataAccess.Entities;
using AvaluxAuth.Models;
using Microsoft.EntityFrameworkCore;

namespace AvaluxAuth.DataAccess.Repositories;

public class SubscriptionRepository(AvaluxAuthDbContext dbContext) : ISubscriptionRepository
{
    public async Task<SubscriptionPlan?> GetPlanByIdAsync(Guid planId, CancellationToken ct = default)
    {
        var entity = await dbContext.SubscriptionPlans
            .Where(e => e.Id == planId)
            .FirstOrDefaultAsync(ct);
        return entity == null ? null : FromEntity(entity);
    }

    public async Task<SubscriptionPlan?> GetPlanByKeyAsync(Guid applicationId, string key,
        CancellationToken ct = default)
    {
        var entity = await dbContext.SubscriptionPlans
            .Where(e => e.ApplicationId == applicationId && e.Key == key)
            .FirstOrDefaultAsync(ct);
        return entity == null ? null : FromEntity(entity);
    }

    public async Task<IEnumerable<SubscriptionPlan>> GetAllPlansAsync(Guid applicationId,
        CancellationToken ct = default)
    {
        var entities = await dbContext.SubscriptionPlans
            .Where(e => e.ApplicationId == applicationId)
            .ToListAsync(ct);
        return entities.Select(FromEntity);
    }

    public async Task<Guid> AddPlanAsync(Guid applicationId, SubscriptionPlanInfo info, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var entity = new SubscriptionPlanEntity
        {
            Id = id,
            ApplicationId = applicationId,
            Key = info.Key,
            DisplayName = info.DisplayName,
            Description = info.Description,
            Advantages = info.Advantages,
            IsDefault = info.IsDefault,
            IsHidden = info.IsHidden,
            PriceCurrency = info.Price.Currency,
            PriceAmount = info.Price.Amount,
            Data = JsonSerializer.Serialize(info.Data),
            CreatedAt = DateTime.UtcNow,
        };
        await dbContext.SubscriptionPlans.AddAsync(entity, ct);
        await dbContext.SaveChangesAsync(ct);
        return id;
    }

    public async Task<bool> UpdatePlanAsync(Guid planId, SubscriptionPlanInfo info, CancellationToken ct = default)
    {
        var count = await dbContext.SubscriptionPlans
            .Where(e => e.Id == planId)
            .ExecuteUpdateAsync(e =>
            {
                e.SetProperty(x => x.Key, info.Key);
                e.SetProperty(x => x.DisplayName, info.DisplayName);
                e.SetProperty(x => x.Description, info.Description);
                e.SetProperty(x => x.Advantages, info.Advantages);
                e.SetProperty(x => x.IsDefault, info.IsDefault);
                e.SetProperty(x => x.IsHidden, info.IsHidden);
                e.SetProperty(x => x.PriceAmount, info.Price.Amount);
                e.SetProperty(x => x.PriceCurrency, info.Price.Currency);
                e.SetProperty(x => x.Data, JsonSerializer.Serialize(info.Data));
            }, ct);
        await dbContext.SaveChangesAsync(ct);
        return count > 0;
    }

    public async Task<Guid> AddUserSubscriptionAsync(Guid userId, Guid planId, DateTime expiredAt, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var entity = new UserSubscriptionEntity
        {
            Id = id,
            UserId = userId,
            PlanId = planId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiredAt,
        };
        await dbContext.UserSubscriptions.AddAsync(entity, ct);
        await dbContext.SaveChangesAsync(ct);
        return id;
    }

    private static SubscriptionPlan FromEntity(SubscriptionPlanEntity entity)
    {
        return new SubscriptionPlan
        {
            Id = entity.Id,
            ApplicationId = entity.ApplicationId,
            Info = new SubscriptionPlanInfo
            {
                Key = entity.Key,
                DisplayName = entity.DisplayName,
                Description = entity.Description,
                Advantages = entity.Advantages,
                IsDefault = entity.IsDefault,
                IsHidden = entity.IsHidden,
                Price = new Money
                {
                    Currency = entity.PriceCurrency,
                    Amount = entity.PriceAmount,
                },
                Data = entity.Data == null ? null : JsonSerializer.Deserialize<object>(entity.Data),
            },
            CreatedAt = entity.CreatedAt,
        };
    }
}