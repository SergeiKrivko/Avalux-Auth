namespace AvaluxAuth.Models;

public class User
{
    public required Guid Id { get; init; }
    public required Guid ApplicationId { get; init; }

    public required DateTime CreatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }
}

public class UserWithAccounts : User
{
    public required AccountInfo[] Accounts { get; init; }
    public UserSubscription[] Subscriptions { get; init; } = [];
}

public class AccountInfo
{
    public required Guid ProviderId { get; init; }
    public required UserInfo UserInfo { get; init; }
}

public class UserWithSubscriptions : User
{
    public SubscriptionInfo[] Subscriptions { get; init; } = [];

    /// <summary>
    /// Current session generation of the user, copied into the access token so a
    /// credential change can invalidate tokens that were issued earlier (M-03).
    /// </summary>
    public int SessionVersion { get; init; }
}

public class SubscriptionInfo
{
    public required string Plan { get; init; }
    public required DateTime ExpiresAt { get; init; }
}