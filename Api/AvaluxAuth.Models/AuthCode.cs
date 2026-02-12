namespace AvaluxAuth.Models;

public class AuthCode
{
    public required string Code { get; init; }
    public required Guid UserId { get; init; }
    public required Guid AccountId { get; init; }
}