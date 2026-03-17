using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Avalux.Auth.UserClient.Models;

namespace Avalux.Auth.UserClient;

public class AuthClient(HttpClient httpClient, string clientId, string clientSecret) : IAuthClient
{
    public UserCredentials? Credentials { get; set; }
    public bool IsAuthenticated => Credentials != null;
    public string? AccessToken => Credentials?.AccessToken;

    public AuthClient(string apiUrl, string clientId, string clientSecret) : this(
        new HttpClient { BaseAddress = new Uri(apiUrl) }, clientId, clientSecret)
    {
    }

    public string GetAuthorizationUrl(string provider, string redirectUrl)
    {
        return $"{httpClient.BaseAddress?.AbsoluteUri}/api/v1/auth/{provider}/authorize?" +
               $"client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(redirectUrl)}";
    }

    public async Task<UserCredentials> GetTokenAsync(string code)
    {
        var resp = await httpClient.PostAsync("api/v1/auth/token", new FormUrlEncodedContent(
            new Dictionary<string, string>()
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "code", code },
            }));
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<UserCredentials>() ?? throw new Exception("Invalid response");
        Credentials = data;
        return data;
    }

    public async Task LinkAccountAsync(string code)
    {
        await RefreshTokenAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/auth/link")
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    { "client_id", clientId },
                    { "client_secret", clientSecret },
                    { "code", code },
                }),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", Credentials.AccessToken) }
        };
        var resp = await httpClient.SendAsync(request);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<UserCredentials> RefreshTokenAsync(string refreshToken)
    {
        var resp = await httpClient.PostAsync("api/v1/auth/refresh", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "refresh_token", refreshToken },
            }));
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<UserCredentials>() ?? throw new Exception("Invalid response");
        return data;
    }

    public async Task<UserCredentials> RefreshTokenAsync(UserCredentials credentials, bool force = false)
    {
        if (force && credentials.ExpiresAt - DateTime.UtcNow > TimeSpan.FromMinutes(1))
            return credentials;
        return await RefreshTokenAsync(credentials.RefreshToken);
    }

    [MemberNotNull(nameof(Credentials))]
    public async Task<UserCredentials> RefreshTokenAsync(bool force = false)
    {
        if (Credentials == null)
            throw new Exception("Not authorized");
        Credentials = await RefreshTokenAsync(Credentials, force);
        return Credentials;
    }

    public async Task RevokeTokenAsync(string refreshToken)
    {
        var resp = await httpClient.PostAsync("api/v1/auth/revoke", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "refresh_token", refreshToken },
            }));
        resp.EnsureSuccessStatusCode();
    }

    public async Task RevokeTokenAsync(UserCredentials credentials)
    {
        await RevokeTokenAsync(credentials.RefreshToken);
    }

    public async Task RevokeTokenAsync()
    {
        if (Credentials == null)
            return;
        await RevokeTokenAsync(Credentials);
        Credentials = null;
    }

    public async Task<UserInfo> GetUserInfoAsync()
    {
        await RefreshTokenAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/auth/userinfo")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", Credentials.AccessToken) }
        };
        var resp = await httpClient.SendAsync(request);
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<UserInfo>() ?? throw new Exception("Invalid response");
        return data;
    }

    public async Task<AccountCredentials> GetAccountCredentialsAsync(string provider)
    {
        await RefreshTokenAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/auth/{provider}/accessToken")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", Credentials.AccessToken) }
        };
        var resp = await httpClient.SendAsync(request);
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<AccountCredentials>() ??
                   throw new Exception("Invalid response");
        return data;
    }
}