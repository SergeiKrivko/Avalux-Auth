using System.ComponentModel.DataAnnotations;

namespace AvaluxAuth.DataAccess.Entities;

internal class RefreshTokenEntity
{
    [MaxLength(256)] public required string RefreshToken { get; init; }
    public required Guid UserId { get; init; }

    public UserEntity User { get; init; } = null!;
}