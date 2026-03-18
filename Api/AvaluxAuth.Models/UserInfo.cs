namespace AvaluxAuth.Models;

public class UserInfo
{
    public required string Id { get; init; }
    public string? Name { get; set; }
    public string? Login { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
}