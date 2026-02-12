namespace AvaluxAuth.Models;

public class AccountCredentials
{
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public DateTime ExpiresAt { get; init; }
}