namespace AvaluxAuth.Models;

public class PasswordUser
{
    public required Guid Id { get; init; }
    public required string Login { get; init; }
    public required PasswordUserInfo Info  { get; init; }
    public required string PasswordHash { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }
}

public class PasswordUserInfo
{
    public string? Name { get; init; }
    public string? Email { get; init; }
    public Guid? AvatarId { get; init; }
}