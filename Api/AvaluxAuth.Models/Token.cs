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

public class TokenPermission
{
    public string Key { get; }
    public string Description { get; }

    private TokenPermission(string key, string description)
    {
        Key = key;
        Description = description;
    }

    public static TokenPermission ReadUserInfo { get; } = new("readUserInfo",
        "Получение данных об аккаунтах пользователя (name, email, avatarUrl) по ID");

    public static TokenPermission SearchUsers { get; } = new("searchUsers",
        "Поиск пользователей по name или email, получение информации о всех пользователях");

    public static TokenPermission DeleteUser { get; } = new("deleteUser", "Удаление пользователя по ID");

    public static TokenPermission ReadUserAccessToken { get; } =
        new("readUserAccessToken", "Получение access-токена провайдера для пользователя");

    public static TokenPermission[] All =>
    [
        ReadUserInfo,
        SearchUsers,
        DeleteUser,
        ReadUserAccessToken
    ];

    public static string[] AllKeys => All.Select(e => e.Key).ToArray();
}