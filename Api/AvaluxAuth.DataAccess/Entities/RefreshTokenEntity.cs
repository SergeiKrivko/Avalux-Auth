using System.ComponentModel.DataAnnotations;

namespace AvaluxAuth.DataAccess.Entities;

internal class RefreshTokenEntity
{
    [MaxLength(256)] public required string RefreshToken { get; init; }
    public required Guid UserId { get; init; }

    /// <summary>OAuth client the token was issued to; a token cannot be used by another client (H-05).</summary>
    [MaxLength(32)] public required string ClientId { get; init; }

    public required DateTime CreatedAt { get; init; }

    /// <summary>Absolute expiry; rotating the token does not extend it (M-03).</summary>
    public required DateTime ExpiresAt { get; init; }

    public UserEntity User { get; init; } = null!;
}