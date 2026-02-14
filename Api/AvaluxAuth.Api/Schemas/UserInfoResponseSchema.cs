using AvaluxAuth.Models;

namespace AvaluxAuth.Api.Schemas;

public class UserInfoResponseSchema
{
    public required Guid Id { get; init; }
    public required AccountInfoSchema[] Accounts { get; init; }
}

public class AccountInfoSchema : UserInfo
{
    public required string Provider { get; init; }
}