namespace AvaluxAuth.Api.Schemas;

public class AddSubscriptionRequestSchema
{
    public required Guid PlanId { get; init; }
    public DateTime? StartsAt { get; init; }
    public required DateTime ExpiresAt { get; init; }
}