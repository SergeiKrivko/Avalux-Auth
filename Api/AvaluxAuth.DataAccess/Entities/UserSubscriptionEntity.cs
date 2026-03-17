namespace AvaluxAuth.DataAccess.Entities;

internal class UserSubscriptionEntity
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required Guid PlanId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? StartsAt { get; init; }
    public required DateTime ExpiresAt { get; init; }

    public UserEntity User { get; init; } = null!;
    public SubscriptionPlanEntity Plan { get; init; } = null!;
}