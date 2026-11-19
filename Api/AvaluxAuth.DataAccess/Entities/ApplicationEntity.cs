using System.ComponentModel.DataAnnotations;

namespace AvaluxAuth.DataAccess.Entities;

internal class ApplicationEntity
{
    public required Guid Id { get; init; }

    [MaxLength(30)] public required string Name { get; init; }
    [MaxLength(32)] public required string ClientId { get; init; }
    [MaxLength(32)] public required string ClientSecret { get; init; }
    public string[] RedirectUrls { get; init; } = [];

    /// <summary>
    /// Public URL of the icon in S3. Null when no icon was uploaded. The object
    /// key is the application id, so an icon can never be orphaned by a replace.
    /// </summary>
    [MaxLength(300)] public string? IconUrl { get; init; }

    /// <summary>
    /// True for confidential clients that must present their client secret at the token endpoint.
    /// Public clients (SPAs, desktop apps) are protected by mandatory PKCE instead (H-05).
    /// </summary>
    public bool RequireClientSecret { get; init; } = true;

    public required DateTime CreatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }

    public ICollection<ProviderEntity> Providers { get; init; } = [];
    public ICollection<TokenEntity> Tokens { get; init; } = [];
    public ICollection<UserEntity> Users { get; init; } = [];
}