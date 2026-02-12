namespace AvaluxAuth.Models;

public class UserCredentials
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public DateTime ExpiresAt { get; init; }
}