using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using AvaluxAuth.Utils;

namespace AvaluxAuth.Providers;

public class YandexAuthProvider(IHttpClientFactory httpClientFactory) : IAuthProvider
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("yandex");

    public string Name => "Яндекс";
    public string Key => "yandex";
    public int Id => 1;

    public string? ProviderUrl => "https://oauth.yandex.ru/";

    public string GetAuthUrl(ProviderParameters config, string redirectUrl, string state)
    {
        ArgumentNullException.ThrowIfNull(config.ClientId);
        ArgumentNullException.ThrowIfNull(config.ClientSecret);

        var builder = new UrlBuilder("https://oauth.yandex.com/authorize")
            .AddQuery("response_type", "code")
            .AddQuery("client_id", config.ClientId)
            .AddQuery("state", state)
            .AddQuery("redirect_uri", redirectUrl);
        return builder.ToString();
    }

    public async Task<AccountCredentials> GetTokenAsync(ProviderParameters config,
        Dictionary<string, string> queryParameters, string redirectUrl, CancellationToken ct)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            { "grant_type", "authorization_code" },
            { "code", queryParameters["code"] },
            { "client_id", config.ClientId },
            { "client_secret", config.ClientSecret },
            { "redirect_uri", redirectUrl }
        });

        var response = await _httpClient.PostAsync("https://oauth.yandex.com/token", content, ct);
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
        if (data == null)
            throw new Exception("Request failed");
        return new AccountCredentials
        {
            AccessToken = data.AccessToken,
            RefreshToken = data.RefreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(data.ExpiresIn)
        };
    }

    private class TokenResponse
    {
        [JsonPropertyName("access_token")] public required string AccessToken { get; set; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }

    public async Task<AccountCredentials> RefreshTokenAsync(ProviderParameters parameters, string refreshToken,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public async Task<UserInfo> GetUserInfoAsync(AccountCredentials credentials, CancellationToken ct)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, "https://login.yandex.ru/info?format=json");
        message.Headers.Authorization = new AuthenticationHeaderValue("OAuth", credentials.AccessToken);
        var response = await _httpClient.SendAsync(message, ct);
        response.EnsureSuccessStatusCode();
        var res = await response.Content.ReadFromJsonAsync<UserInfoResponse>(ct);
        if (res == null)
            throw new Exception("Request failed");
        return new UserInfo
        {
            Id = res.Id.ToString(),
            Name = res.RealName,
            Email = res.Email,
            AvatarUrl = res.AvatarId == null ? null : $"https://avatars.yandex.net/get-yapic/{res.AvatarId}/islands-68",
        };
    }

    private class UserInfoResponse
    {
        [JsonPropertyName("id")] public required int Id { get; set; }
        [JsonPropertyName("real_name")] public string? RealName { get; set; }
        [JsonPropertyName("default_email")] public string? Email { get; set; }

        [JsonPropertyName("default_avatar_id")]
        public string? AvatarId { get; set; }
    }
}