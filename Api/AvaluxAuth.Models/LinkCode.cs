namespace AvaluxAuth.Models;

public class LinkCode
{
    public required string Code { get; init; }
    public required Guid UserId { get; init; }
}