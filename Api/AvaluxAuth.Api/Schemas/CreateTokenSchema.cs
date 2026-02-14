namespace AvaluxAuth.Api.Schemas;

public class CreateTokenSchema
{
    public required string Name { get; init; }
    public required string[] Permissions { get; init; } = [];
    public required DateTime ExpiresAt { get; init; }
}