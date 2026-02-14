using System.ComponentModel.DataAnnotations;

namespace AvaluxAuth.DataAccess.Entities;

internal class TokenEntity
{
    public required Guid Id { get; init; }
    public required Guid ApplicationId { get; init; }
    [MaxLength(100)] public string? Name { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public DateTime? DeletedAt { get; init; }
    public string[] Permissions { get; init; } = [];

    public ApplicationEntity Application { get; init; } = null!;
}