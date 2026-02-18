using System.Text.Json.Serialization;

namespace AvaluxAuth.Models;

public class AccountCredentials
{
    public string? AccessToken { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefreshToken { get; init; }

    public DateTime ExpiresAt { get; init; }
}