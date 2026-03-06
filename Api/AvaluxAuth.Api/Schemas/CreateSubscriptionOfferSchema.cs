namespace AvaluxAuth.Api.Schemas;

public class CreateSubscriptionOfferSchema
{
    public required int DurationSeconds { get; init; }
    public required decimal Price { get; init; }
    public required string Currency { get; init; }
}