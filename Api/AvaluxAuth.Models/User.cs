namespace AvaluxAuth.Models;

public class User
{
    public required Guid Id { get; init; }
    public required Guid ApplicationId { get; init; }

    public required DateTime CreatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }
}