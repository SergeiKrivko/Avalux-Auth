using AvaluxAuth.Models;

namespace AvaluxAuth.Api.Schemas;

public class UserInfoResponseSchema
{
    public required Guid Id { get; init; }
    public required AccountInfo[] Accounts { get; init; }
}

public class AccountInfo : UserInfo
{
    public required string Provider { get; init; }
}