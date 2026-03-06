namespace AvaluxAuth.Models;

public class UserSubscription
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required Guid PlanId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime ExpiresAt { get; init; }
}