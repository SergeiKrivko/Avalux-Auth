namespace Avalux.Auth.UserClient.Models;

public class AccountCredentials
{
    public required string AccessToken { get; init; }

    public DateTime ExpiresAt { get; init; }
}