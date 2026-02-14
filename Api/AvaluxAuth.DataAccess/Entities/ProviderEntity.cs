using System.ComponentModel.DataAnnotations;

namespace AvaluxAuth.DataAccess.Entities;

internal class ProviderEntity
{
    public Guid Id { get; init; }
    public Guid ApplicationId { get; init; }
    public int ProviderId { get; init; }

    [MaxLength(100)] public string? ClientId { get; init; }
    [MaxLength(100)] public string? ClientSecret { get; init; }
    public bool SaveTokens { get; init; }
    public string[] DefaultScope { get; init; } = [];

    public required DateTime CreatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }

    public ApplicationEntity Application { get; init; } = null!;
    public ICollection<AccountEntity> Accounts { get; init; } = [];
}