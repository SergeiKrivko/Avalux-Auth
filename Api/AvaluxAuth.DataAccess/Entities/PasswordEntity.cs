using System.ComponentModel.DataAnnotations;

namespace AvaluxAuth.DataAccess.Entities;

public class PasswordEntity
{
    public required Guid Id { get; init; }
    [MaxLength(64)] public required string Login { get; init; }
    [MaxLength(256)] public required string PasswordHash { get; init; }
    [MaxLength(64)] public string? Name { get; init; }
    [MaxLength(64)] public string? Email { get; init; }

    /// <summary>
    /// When the address was confirmed through the link from the letter (R-02 in
    /// SECURITY-AUDIT.md). Null for accounts without an address and after the
    /// address has been changed, until the new one is confirmed.
    /// </summary>
    public DateTime? EmailConfirmedAt { get; init; }

    public Guid? AvatarId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }
}