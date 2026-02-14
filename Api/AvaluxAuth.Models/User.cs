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
}

public class AccountInfo
{
    public required Guid ProviderId { get; init; }
    public required UserInfo UserInfo { get; init; }
}