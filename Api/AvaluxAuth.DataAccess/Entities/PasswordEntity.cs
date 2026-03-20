using System.ComponentModel.DataAnnotations;

namespace AvaluxAuth.DataAccess.Entities;

public class PasswordEntity
{
    public required Guid Id { get; init; }
    [MaxLength(64)] public required string Login { get; init; }
    [MaxLength(256)] public required string PasswordHash { get; init; }
    [MaxLength(64)] public string? Name { get; init; }
    [MaxLength(64)] public string? Email { get; init; }
    public Guid? AvatarId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }
}