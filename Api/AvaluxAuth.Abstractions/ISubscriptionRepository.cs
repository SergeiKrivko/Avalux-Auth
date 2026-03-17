using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface ISubscriptionRepository
{
    public Task<SubscriptionPlan?> GetPlanByIdAsync(Guid planId, CancellationToken ct = default);
    public Task<SubscriptionPlan?> GetPlanByKeyAsync(Guid applicationId, string key, CancellationToken ct = default);
    public Task<IEnumerable<SubscriptionPlan>> GetAllPlansAsync(Guid applicationId, CancellationToken ct = default);
    public Task<Guid> AddPlanAsync(Guid applicationId, SubscriptionPlanInfo info, CancellationToken ct = default);
    public Task<bool> UpdatePlanAsync(Guid planId, SubscriptionPlanInfo info, CancellationToken ct = default);

    public Task<Guid> AddUserSubscriptionAsync(Guid userId, Guid planId, DateTime? startsAt, DateTime expiredAt,
        CancellationToken ct = default);
}