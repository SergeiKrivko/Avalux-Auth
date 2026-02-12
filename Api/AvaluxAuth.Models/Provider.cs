namespace AvaluxAuth.Models;

public class Provider
{
    public Guid Id { get; init; }
    public Guid ApplicationId { get; init; }
    public int ProviderId { get; init; }
    public required ProviderParameters Parameters { get; init; }

    public required DateTime CreatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }
}