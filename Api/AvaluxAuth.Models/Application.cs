namespace AvaluxAuth.Models;

public class Application
{
    public required Guid Id { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required ApplicationParameters Parameters { get; init; }

    public required DateTime CreatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }
}