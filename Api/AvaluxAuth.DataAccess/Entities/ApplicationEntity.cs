using System.ComponentModel.DataAnnotations;

namespace AvaluxAuth.DataAccess.Entities;

internal class ApplicationEntity
{
    public required Guid Id { get; init; }

    [MaxLength(30)] public required string Name { get; init; }
    [MaxLength(32)] public required string ClientId { get; init; }
    [MaxLength(32)] public required string ClientSecret { get; init; }
    public string[] RedirectUrls { get; init; } = [];

    public required DateTime CreatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }

    public ICollection<ProviderEntity> Providers { get; init; } = [];
}