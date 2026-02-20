using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using AvaluxAuth.Utils;

namespace AvaluxAuth.Providers;

public class GoogleAuthProvider(IHttpClientFactory httpClientFactory) : IAuthProvider
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("google");

    public string Name => "Google";
    public string Key => "google";
    public int Id => 2;
    public string ProviderUrl => "https://console.cloud.google.com/apis";

    public string GetAuthUrl(ProviderParameters config, string redirectUrl, string state)
    {
        ArgumentNullException.ThrowIfNull(config.ClientId);
        ArgumentNullException.ThrowIfNull(config.ClientSecret);

        Console.WriteLine(redirectUrl);
        var builder = new UrlBuilder("https://accounts.google.com/o/oauth2/v2/auth")
            .AddQuery("response_type", "code")
            .AddQuery("client_id", config.ClientId)
            .AddQuery("redirect_uri", redirectUrl)
            .AddQuery("scope", string.Join(' ', config.DefaultScope))
            .AddQuery("state", state);

        Console.WriteLine(builder.ToString());
        return builder.ToString();
    }

    public async Task<AccountCredentials> GetTokenAsync(ProviderParameters config,
        Dictionary<string, string> queryParameters, string redirectUrl, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(config.ClientId);
        ArgumentNullException.ThrowIfNull(config.ClientSecret);

        var content = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            { "client_id", config.ClientId },
            { "client_secret", config.ClientSecret },
            { "grant_type", "authorization_code" },
            { "code", queryParameters["code"] },
            { "redirect_uri", redirectUrl }
        });

        var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", content, ct);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<TokenResponse>(ct) ??
                   throw new Exception("Invalid response");
        return new AccountCredentials
        {
            AccessToken = data.AccessToken,
            RefreshToken = data.RefreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(data.ExpiresIn)
        };
    }

    public async Task<AccountCredentials> RefreshTokenAsync(ProviderParameters config, string refreshToken,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(config.ClientId);
        ArgumentNullException.ThrowIfNull(config.ClientSecret);

        var content = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            { "client_id", config.ClientId },
            { "client_secret", config.ClientSecret },
            { "grant_type", "authorization_code" },
            { "refresh_token", refreshToken },
        });

        var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", content, ct);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<TokenResponse>(ct) ??
                   throw new Exception("Invalid response");
        return new AccountCredentials
        {
            AccessToken = data.AccessToken,
            RefreshToken = data.RefreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(data.ExpiresIn)
        };
    }

    public async Task<bool> RevokeTokenAsync(ProviderParameters parameters, AccountCredentials credentials, CancellationToken ct = default)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            { "token", credentials.RefreshToken },
        });

        var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/revoke", content, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<UserInfo> GetUserInfoAsync(AccountCredentials credentials, CancellationToken ct)
    {
        var http = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
        http.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
        var response = await _httpClient.SendAsync(http, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<UserInfoResponse>(ct) ??
                   throw new Exception("Invalid response");
        return new UserInfo
        {
            Id = json.Sub,
            Name = json.Name,
            Email = json.Email,
            AvatarUrl = json.Picture,
        };
    }

    private class TokenResponse
    {
        [JsonPropertyName("access_token")] public required string AccessToken { get; init; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; init; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; init; }
    }

    private class UserInfoResponse
    {
        [JsonPropertyName("sub")] public required string Sub { get; init; }
        [JsonPropertyName("name")] public string? Name { get; init; }
        [JsonPropertyName("email")] public string? Email { get; init; }

        [JsonPropertyName("picture")] public string? Picture { get; init; }
    }
}