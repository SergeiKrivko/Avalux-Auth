using System.ComponentModel.DataAnnotations;

namespace AvaluxAuth.DataAccess.Entities;

internal sealed class SigningKeyEntity
{
    public Guid Id { get; init; }

    [MaxLength(100)] public string Kid { get; init; } = string.Empty;

    [MaxLength(50)] public string Algorithm { get; init; } = string.Empty;

    [MaxLength(10)] public string Use { get; init; } = "sig";

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; init; }

    public bool IsActive { get; init; }

    [MaxLength(8000)] public string PrivateKeyEncrypted { get; init; } = string.Empty;

    [MaxLength(8000)] public string PublicJwkJson { get; init; } = string.Empty;
}