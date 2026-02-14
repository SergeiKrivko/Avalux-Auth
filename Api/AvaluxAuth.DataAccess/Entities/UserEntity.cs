namespace AvaluxAuth.DataAccess.Entities;

internal class UserEntity
{
    public required Guid Id { get; init; }
    public required Guid ApplicationId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }

    public ApplicationEntity Application { get; init; } = null!;
    public ICollection<AccountEntity> Accounts { get; init; } = [];
    public ICollection<RefreshTokenEntity> RefreshTokens { get; init; } = [];
}