using AvaluxAuth.Models;

namespace AvaluxAuth.Api.Schemas;

public class UsersResponseSchema
{
    public required int Total { get; init; }
    public int Page { get; init; }
    public int? Limit { get; init; }
    public required IEnumerable<UserWithAccounts> Users { get; init; }
}