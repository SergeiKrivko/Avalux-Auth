using System.Net.Http.Json;
using AvaluxAuth.Models;
using AvaluxAuth.TestCli.Schemas;

namespace AvaluxAuth.TestCli;

public class ApiClient(string apiUrl = "http://localhost:5000")
{
    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri(apiUrl),
    };

    public void StartAuthorization(string providerKey, string clientId)
    {
        Utils.OpenUrl(_httpClient.BaseAddress +
                      $"api/v1/auth/{providerKey}/authorize?client_id={clientId}&redirect_uri={Utils.CallbackUrl}");
    }

    public async Task<UserCredentials> GetAccessToken(string code, string clientId, string clientSecret)
    {
        var parameters = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "code", code },
        };
        var response = await _httpClient.PostAsync("api/v1/auth/token", new FormUrlEncodedContent(parameters));
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<UserCredentials>();
        return data ?? throw new Exception("Empty response");
    }

    public async Task LinkAccount(string code, string clientId, string clientSecret, UserCredentials credentials)
    {
        var parameters = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "code", code },
        };
        var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, "api/v1/auth/link")
        {
            Content = new FormUrlEncodedContent(parameters),
            Headers = { { "Authorization", $"Bearer {credentials.AccessToken}" } }
        });
        response.EnsureSuccessStatusCode();
    }

    public async Task<UserCredentials> RefreshToken(UserCredentials credentials)
    {
        if (credentials.ExpiresAt > DateTimeOffset.UtcNow)
            return credentials;
        var parameters = new Dictionary<string, string>
        {
            { "refresh_token", credentials.RefreshToken ?? throw new Exception("Empty refresh token") },
        };
        var response = await _httpClient.PostAsync("api/v1/auth/refresh", new FormUrlEncodedContent(parameters));
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<UserCredentials>();
        return data ?? throw new Exception("Empty response");
    }

    public async Task<UserInfoResponseSchema> GetUserInfo(UserCredentials credentials)
    {
        var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "api/v1/auth/userinfo")
        {
            Headers = { { "Authorization", $"Bearer {credentials.AccessToken}" } }
        });
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<UserInfoResponseSchema>();
        return data ?? throw new Exception("Empty response");
    }
}