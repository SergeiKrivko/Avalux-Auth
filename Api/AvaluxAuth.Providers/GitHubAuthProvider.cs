using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using AvaluxAuth.Utils;

namespace AvaluxAuth.Providers;

public class GitHubAuthProvider(IHttpClientFactory httpClientFactory) : IAuthProvider
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("github");

    public string Name => "GitHub";
    public string Key => "github";
    public int Id => 3;
    public string ProviderUrl => "https://github.com/settings/developers";

    public string GetAuthUrl(ProviderParameters config, string redirectUrl, string state)
    {
        ArgumentNullException.ThrowIfNull(config.ClientId);
        ArgumentNullException.ThrowIfNull(config.ClientSecret);

        Console.WriteLine(redirectUrl);
        var builder = new UrlBuilder("https://github.com/login/oauth/authorize")
            .AddQuery("response_type", "code")
            .AddQuery("client_id", config.ClientId)
            .AddQuery("redirect_uri", redirectUrl)
            .AddQuery("scope", string.Join(' ', config.DefaultScope))
            .AddQuery("state", state);

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
            { "code", queryParameters["code"] },
            { "redirect_uri", redirectUrl }
        });

        var response = await _httpClient.PostAsync("https://github.com/login/oauth/access_token", content, ct);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<TokenResponse>(ct) ??
                   throw new Exception("Invalid response");
        return new AccountCredentials
        {
            AccessToken = data.AccessToken,
            ExpiresAt = DateTime.UtcNow.AddYears(1)
        };
    }

    public Task<AccountCredentials> RefreshTokenAsync(ProviderParameters config, string refreshToken,
        CancellationToken ct)
    {
        throw new NotSupportedException("GitHub does not support refresh tokens for OAuth Apps");
    }

    public Task<bool> RevokeTokenAsync(ProviderParameters parameters, AccountCredentials credentials, CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }

    public async Task<UserInfo> GetUserInfoAsync(AccountCredentials credentials, CancellationToken ct)
    {
        var http = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
        http.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
        var response = await _httpClient.SendAsync(http, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<UserInfoResponse>(ct) ??
                   throw new Exception("Invalid response");
        return new UserInfo
        {
            Id = json.Id,
            Name = json.Name,
            Email = json.Email,
            AvatarUrl = json.AvatarUrl,
        };
    }

    private class TokenResponse
    {
        [JsonPropertyName("access_token")] public required string AccessToken { get; init; }
    }

    private class UserInfoResponse
    {
        [JsonPropertyName("id")] public required string Id { get; init; }
        [JsonPropertyName("name")] public string? Name { get; init; }
        [JsonPropertyName("email")] public string? Email { get; init; }
        [JsonPropertyName("avatar_url")] public string? AvatarUrl { get; init; }
    }
}