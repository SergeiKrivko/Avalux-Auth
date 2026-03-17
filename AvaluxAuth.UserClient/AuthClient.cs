using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Avalux.Auth.UserClient.Models;

namespace Avalux.Auth.UserClient;

public class AuthClient(HttpClient httpClient, string clientId, string clientSecret = "") : IAuthClient
{
    public UserCredentials? Credentials { get; set; }
    public bool IsAuthenticated => Credentials != null;
    public string? AccessToken => Credentials?.AccessToken;

    public AuthClient(string apiUrl, string clientId, string clientSecret = "") : this(
        new HttpClient { BaseAddress = new Uri(apiUrl) }, clientId, clientSecret)
    {
    }

    public string GetAuthorizationUrl(string provider, string redirectUrl)
    {
        return $"{httpClient.BaseAddress?.AbsoluteUri}api/v1/auth/authorize?provider={provider}" +
               $"&client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(redirectUrl)}";
    }

    public async Task<UserCredentials> GetTokenAsync(string code, CancellationToken ct = default)
    {
        var resp = await httpClient.PostAsync("api/v1/auth/token", new FormUrlEncodedContent(
            new Dictionary<string, string>()
            {
                { "grant_type", "authorization_code" },
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "code", code },
            }), ct);
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<UserCredentialsSchema>(ct) ??
                   throw new Exception("Invalid response");
        Credentials = UserCredentials.FromSchema(data);
        return Credentials;
    }

    public async Task LinkAccountAsync(string code, CancellationToken ct = default)
    {
        await RefreshTokenAsync(false, ct);
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
        var resp = await httpClient.SendAsync(request, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<UserCredentials> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var resp = await httpClient.PostAsync("api/v1/auth/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "refresh_token", refreshToken },
            }), ct);
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<UserCredentialsSchema>(ct) ??
                   throw new Exception("Invalid response");
        return UserCredentials.FromSchema(data);
    }

    public async Task<UserCredentials> RefreshTokenAsync(UserCredentials credentials, bool force = false,
        CancellationToken ct = default)
    {
        if (force && credentials.ExpiresAt - DateTime.UtcNow > TimeSpan.FromMinutes(1))
            return credentials;
        return await RefreshTokenAsync(credentials.RefreshToken, ct);
    }

    [MemberNotNull(nameof(Credentials))]
    public async Task<UserCredentials> RefreshTokenAsync(bool force = false, CancellationToken ct = default)
    {
        if (Credentials == null)
            throw new Exception("Not authorized");
        Credentials = await RefreshTokenAsync(Credentials, force, ct);
        return Credentials;
    }

    public async Task RevokeTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var resp = await httpClient.PostAsync("api/v1/auth/revoke", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "refresh_token", refreshToken },
            }), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task RevokeTokenAsync(UserCredentials credentials, CancellationToken ct = default)
    {
        await RevokeTokenAsync(credentials.RefreshToken, ct);
    }

    public async Task RevokeTokenAsync(CancellationToken ct = default)
    {
        if (Credentials == null)
            return;
        await RevokeTokenAsync(Credentials, ct);
        Credentials = null;
    }

    public async Task<UserInfo> GetUserInfoAsync(CancellationToken ct = default)
    {
        await RefreshTokenAsync(false, ct);
        var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/auth/userinfo")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", Credentials.AccessToken) }
        };
        var resp = await httpClient.SendAsync(request, ct);
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<UserInfo>(ct) ?? throw new Exception("Invalid response");
        return data;
    }

    public async Task<AccountCredentials> GetAccountCredentialsAsync(string provider, CancellationToken ct = default)
    {
        await RefreshTokenAsync(false, ct);
        var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/auth/{provider}/accessToken")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", Credentials.AccessToken) }
        };
        var resp = await httpClient.SendAsync(request, ct);
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<AccountCredentials>(ct) ??
                   throw new Exception("Invalid response");
        return data;
    }

    public async Task<UserCredentials> AuthorizeInstalledAsync(string provider, string redirectUrl,
        CancellationToken ct = default)
    {
        var url = GetAuthorizationUrl(provider, redirectUrl);
        OpenUrl(url);
        var code = await ReceiveAuthCodeAsync(redirectUrl, ct);
        if (code == null)
            throw new Exception("Authorization failed");
        return await GetTokenAsync(code, ct);
    }

    public async Task LinkInstalledAsync(string provider, string redirectUrl, CancellationToken ct = default)
    {
        await RefreshTokenAsync(true, ct);
        var url = GetAuthorizationUrl(provider, redirectUrl);
        OpenUrl(url);
        var code = await ReceiveAuthCodeAsync(redirectUrl, ct);
        if (code == null)
            throw new Exception("Authorization failed");
        await LinkAccountAsync(code, ct);
    }

    private static void OpenUrl(string url)
    {
        if (OperatingSystem.IsWindows())
            Process.Start(new ProcessStartInfo
                { FileName = "cmd", Arguments = $"/c start {url[0]}\"{url.AsSpan(1)}\"", CreateNoWindow = true });
        else if (OperatingSystem.IsLinux())
            Process.Start(new ProcessStartInfo
                { FileName = "xdg-open", Arguments = $"\"{url}\"", CreateNoWindow = true });
        else if (OperatingSystem.IsMacOS())
            Process.Start(new ProcessStartInfo { FileName = "open", Arguments = $"\"{url}\"", CreateNoWindow = true });
    }

    private static async Task<string?> ReceiveAuthCodeAsync(string redirectUrl, CancellationToken ct)
    {
        // Запускаем локальный HTTP-сервер
        var listener = new HttpListener();
        ct.Register(() => listener.Stop());
        if (!redirectUrl.EndsWith('/'))
            redirectUrl += '/';
        listener.Prefixes.Add(redirectUrl);
        listener.Start();

        // Ждем входящий запрос
        var context = await listener.GetContextAsync();
        var request = context.Request;
        // Отправляем ответ клиенту
        var response = context.Response;
        var bytes = Encoding.UTF8.GetBytes("Code received. Close this page");
        response.ContentLength64 = bytes.Length;
        response.ContentEncoding = Encoding.UTF8;
        await using (var output = response.OutputStream)
        {
            await output.WriteAsync(bytes, ct);
            output.Close();
        }

        listener.Stop();

        return request.QueryString.Get("code");
    }
}