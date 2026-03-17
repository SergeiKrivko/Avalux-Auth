namespace Avalux.Auth.ApiClient.Models;

public class AccountCredentials
{
    public required string AccessToken { get; init; }

    public DateTime ExpiresAt { get; init; }
}