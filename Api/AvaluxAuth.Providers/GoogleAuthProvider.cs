using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using AvaluxAuth.Utils;
using Microsoft.Extensions.Logging;

namespace AvaluxAuth.Providers;

public class GoogleAuthProvider(IHttpClientFactory httpClientFactory, ILogger<GoogleAuthProvider> logger)
    : IAuthProvider
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

        var authUrlBuilder = new UrlBuilder("https://accounts.google.com/o/oauth2/v2/auth")
            .AddQuery("response_type", "code")
            .AddQuery("client_id", config.ClientId)
            .AddQuery("redirect_uri", redirectUrl)
            .AddQuery("scope", string.Join(' ', config.DefaultScope))
            .AddQuery("state", state);

        // L-05: Google issues a refresh token only for an offline grant, and only when
        // it is requested. Without this the consent flow returns access tokens alone,
        // so there is nothing for RefreshTokenAsync to use later.
        if (config.SaveTokens)
            authUrlBuilder.AddQuery("access_type", "offline");

        var authUrl = authUrlBuilder.ToString();

        // M-05: the URL carries the one-time state (and client_id), so only the
        // redirect URI — public configuration — is logged.
        logger.LogDebug("Google authorization URL built: redirectUri={RedirectUri}", redirectUrl);

        return authUrl;
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
            // L-05: a refresh must be sent as grant_type=refresh_token. With
            // authorization_code Google rejected every refresh, so provider tokens
            // expired and could not be renewed.
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken },
        });

        var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", content, ct);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<TokenResponse>(ct) ??
                   throw new Exception("Invalid response");
        return new AccountCredentials
        {
            AccessToken = data.AccessToken,
            // Google does not echo the refresh token on a refresh. Falling back to the
            // one just used keeps the stored credentials usable: UserService persists
            // this object, and a null here would erase the refresh token for good.
            RefreshToken = string.IsNullOrEmpty(data.RefreshToken) ? refreshToken : data.RefreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(data.ExpiresIn)
        };
    }

    public async Task<bool> RevokeTokenAsync(ProviderParameters parameters, AccountCredentials credentials,
        CancellationToken ct = default)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            { "token", credentials.RefreshToken },
        });

        var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/revoke", content, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<UserInfo> GetUserInfoAsync(ProviderParameters config, AccountCredentials credentials,
        CancellationToken ct)
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