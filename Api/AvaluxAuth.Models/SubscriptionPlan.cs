namespace AvaluxAuth.Models;

public class SubscriptionPlan
{
    public required Guid Id { get; init; }
    public required Guid ApplicationId { get; init; }
    public required SubscriptionPlanInfo Info { get; init; }

    public required DateTime CreatedAt { get; init; }
}

public class SubscriptionPlanInfo
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public string[] Advantages { get; init; } = [];
    public bool IsHidden { get; init; }
    public bool IsDefault { get; init; }

    /// <summary>
    /// Стоимость подписки за 1 день
    /// </summary>
    public required Money Price { get; init; }

    /// <summary>
    /// Данные о тарифах в формате json
    /// </summary>
    public object? Data { get; init; }
}