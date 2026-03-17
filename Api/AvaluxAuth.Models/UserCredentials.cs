using System.Text.Json.Serialization;

namespace AvaluxAuth.Models;

public class UserCredentials
{
    [JsonPropertyName("access_token")] public required string AccessToken { get; init; }
    [JsonPropertyName("refresh_token")] public required string RefreshToken { get; init; }
    [JsonIgnore] public DateTime ExpiresAt { get; init; }

    [JsonPropertyName("expires_in")] public double ExpiresIn => (ExpiresAt - DateTime.UtcNow).TotalSeconds;

    [JsonPropertyName("token_type")] public string TokenType => "Bearer";
}