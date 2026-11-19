namespace AvaluxAuth.DataAccess.Entities;

internal class UserEntity
{
    public required Guid Id { get; init; }
    public required Guid ApplicationId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }

    /// <summary>
    /// Bumped whenever the user's credentials change (password change, linked
    /// account); access tokens carrying an older value are rejected and all
    /// refresh tokens are dropped (M-03).
    /// </summary>
    public int SessionVersion { get; init; }

    public ApplicationEntity Application { get; init; } = null!;
    public ICollection<AccountEntity> Accounts { get; init; } = [];
    public ICollection<RefreshTokenEntity> RefreshTokens { get; init; } = [];
    public ICollection<UserSubscriptionEntity> Subscriptions { get; init; } = [];
}