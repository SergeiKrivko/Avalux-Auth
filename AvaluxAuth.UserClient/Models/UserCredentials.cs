using System.Text.Json.Serialization;

namespace Avalux.Auth.UserClient.Models;

public class UserCredentials
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public DateTime ExpiresAt { get; init; }

    internal static UserCredentials FromSchema(UserCredentialsSchema schema)
    {
        return new UserCredentials
        {
            AccessToken = schema.AccessToken,
            RefreshToken = schema.RefreshToken,
            ExpiresAt = DateTime.Now.AddSeconds(schema.ExpiresIn),
        };
    }
}

internal class UserCredentialsSchema
{
    [JsonPropertyName("access_token")] public required string AccessToken { get; init; }
    [JsonPropertyName("refresh_token")] public required string RefreshToken { get; init; }
    [JsonPropertyName("expires_in")] public double ExpiresIn { get; init; }
}