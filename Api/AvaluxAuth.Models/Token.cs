namespace AvaluxAuth.Models;

public class Token
{
    public required Guid Id { get; init; }
    public required Guid ApplicationId { get; init; }
    public string? Name { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public DateTime? DeletedAt { get; init; }
    public string[] Permissions { get; init; } = [];
}

public static class TokenPermissions
{
    public const string ReadUserInfo = "readUserInfo";
    public const string DeleteUser = "deleteUser";
    public const string ReadUserAccessToken = "readUserAccessToken";

    public static string[] All =>
    [
        ReadUserInfo,
        DeleteUser,
        ReadUserAccessToken
    ];
}