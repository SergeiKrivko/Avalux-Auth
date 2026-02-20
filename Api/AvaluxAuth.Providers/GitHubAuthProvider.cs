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

        var response = await _httpClient.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string?>
                {
                    { "client_id", config.ClientId },
                    { "client_secret", config.ClientSecret },
                    { "code", queryParameters["code"] },
                    { "redirect_uri", redirectUrl }
                }),
                Headers = { { "Accept", "application/json" } }
            }, ct);
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

    public Task<bool> RevokeTokenAsync(ProviderParameters parameters, AccountCredentials credentials,
        CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }

    public async Task<UserInfo> GetUserInfoAsync(ProviderParameters config, AccountCredentials credentials,
        CancellationToken ct)
    {
        var response = await GetAsync("https://api.github.com/user", config, credentials, ct);
        var json = await response.Content.ReadFromJsonAsync<UserInfoResponse>(ct) ??
                   throw new Exception("Invalid response");

        var email = json.Email;
        if (email is null)
        {
            var emailsResponse = await GetAsync("https://api.github.com/user/emails", config, credentials, ct);
            if (emailsResponse.IsSuccessStatusCode)
            {
                var emails = await emailsResponse.Content.ReadFromJsonAsync<EmailResponse[]>(ct) ?? [];
                email = emails.FirstOrDefault(e => e.IsPrimary)?.Email;
            }
        }

        return new UserInfo
        {
            Id = json.Id.ToString(),
            Name = json.Name ?? json.Login,
            Email = email,
            AvatarUrl = json.AvatarUrl,
        };
    }

    private async Task<HttpResponseMessage> GetAsync(string url, ProviderParameters config,
        AccountCredentials credentials, CancellationToken ct)
    {
        var http = new HttpRequestMessage(HttpMethod.Get, url)
        {
            Headers =
            {
                { "Authorization", "Bearer " + credentials.AccessToken },
                { "User-Agent", config.ClientName },
            }
        };
        var response = await _httpClient.SendAsync(http, ct);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private class TokenResponse
    {
        [JsonPropertyName("access_token")] public required string AccessToken { get; init; }
    }

    private class EmailResponse
    {
        [JsonPropertyName("email")] public required string Email { get; init; }
        [JsonPropertyName("verified")] public bool IsVerified { get; init; }
        [JsonPropertyName("primary")] public bool IsPrimary { get; init; }
    }

    private class UserInfoResponse
    {
        [JsonPropertyName("id")] public required int Id { get; init; }
        [JsonPropertyName("login")] public string? Login { get; init; }
        [JsonPropertyName("name")] public string? Name { get; init; }
        [JsonPropertyName("email")] public string? Email { get; init; }
        [JsonPropertyName("avatar_url")] public string? AvatarUrl { get; init; }
    }
}