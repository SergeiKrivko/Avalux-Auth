namespace AvaluxAuth.Models;

public class UserInfo
{
    public required string Id { get; init; }
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? AvatarUrl { get; init; }
}