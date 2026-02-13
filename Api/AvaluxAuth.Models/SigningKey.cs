using System.ComponentModel.DataAnnotations;

namespace AvaluxAuth.Models;

public sealed class SigningKey
{
    public Guid Id { get; set; }

    [MaxLength(100)] public string Kid { get; set; } = string.Empty;

    [MaxLength(50)] public string Algorithm { get; set; } = string.Empty;

    [MaxLength(10)] public string Use { get; set; } = "sig";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    public bool IsActive { get; set; }

    [MaxLength(8000)] public string PrivateKeyEncrypted { get; set; } = string.Empty;

    public string PublicJwkJson { get; set; } = string.Empty;
}