using System.ComponentModel.DataAnnotations;

namespace AvaluxAuth.DataAccess.Entities;

internal class AccountEntity
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required Guid ProviderId { get; init; }
    [MaxLength(50)] public required string ProviderUserId { get; init; }

    [MaxLength(50)] public string? Name { get; init; }
    [MaxLength(50)] public string? Email { get; init; }
    [MaxLength(200)] public string? AvatarUrl { get; init; }

    [MaxLength(200)] public string? AccessToken { get; init; }
    [MaxLength(200)] public string? RefreshToken { get; init; }
    public DateTime ExpiresAt { get; init; }

    public required DateTime CreatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }

    public UserEntity User { get; init; } = null!;
    public ProviderEntity Provider { get; init; } = null!;
}