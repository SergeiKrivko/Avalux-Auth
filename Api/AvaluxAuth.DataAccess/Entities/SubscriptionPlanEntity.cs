using System.ComponentModel.DataAnnotations;

namespace AvaluxAuth.DataAccess.Entities;

internal class SubscriptionPlanEntity
{
    public required Guid Id { get; init; }
    public required Guid ApplicationId { get; init; }
    [MaxLength(32)] public required string Key { get; init; }

    [MaxLength(64)] public required string DisplayName { get; init; }
    [MaxLength(1024)] public string? Description { get; init; }
    public string[] Advantages { get; init; } = [];

    public bool IsHidden { get; init; }
    public bool IsDefault { get; init; }
    [MaxLength(10)] public required string PriceCurrency { get; init; }
    public required decimal PriceAmount { get; init; }

    public required DateTime CreatedAt { get; init; }

    public ApplicationEntity Application { get; init; } = null!;
    public ICollection<UserSubscriptionEntity> Subscriptions { get; init; } = [];
}