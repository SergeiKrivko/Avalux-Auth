using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Avalux.Auth.UserClient.Models;

namespace Avalux.Auth.UserClient;

public class AuthClient(
    HttpClient httpClient,
    string clientId,
    string clientSecret = "",
    ICredentialsStore? credentialsStore = null) : IAuthClient
{
    public UserCredentials? Credentials { get; private set; }

    // PKCE state for the authorization in flight (H-05/C-02). The desktop client is a public
    // client: the code is bound to the verifier, and `state` protects against a forged redirect.
    private string? _codeVerifier;
    private string? _pendingState;
    private string? _pendingRedirectUrl;
    public bool IsAuthenticated => Credentials != null;
    public string? AccessToken => Credentials?.AccessToken;

    public AuthClient(string apiUrl, string clientId, string clientSecret = "",
        ICredentialsStore? credentialsStore = null) : this(
        new HttpClient { BaseAddress = new Uri(apiUrl) }, clientId, clientSecret, credentialsStore)
    {
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string GetAuthorizationUrl(string provider, string redirectUrl)
    {
        return BuildAuthorizationUrl(provider, redirectUrl);
    }

    public async Task<string> GetLinkUrl(string provider, string redirectUrl, CancellationToken ct = default)
    {
        return BuildAuthorizationUrl(provider, redirectUrl, await GetLinkCodeAsync(ct));
    }

    private string BuildAuthorizationUrl(string provider, string redirectUrl, string? linkCode = null)
    {
        var verifier = Pkce.CreateCodeVerifier();
        var state = Pkce.CreateState();
        _codeVerifier = verifier;
        _pendingState = state;
        _pendingRedirectUrl = redirectUrl;

        var url = $"{httpClient.BaseAddress?.AbsoluteUri}api/v1/auth/authorize" +
                  $"?provider={Uri.EscapeDataString(provider)}" +
                  $"&client_id={Uri.EscapeDataString(clientId)}" +
                  $"&redirect_uri={Uri.EscapeDataString(redirectUrl)}" +
                  $"&state={Uri.EscapeDataString(state)}" +
                  $"&code_challenge={Pkce.CreateCodeChallenge(verifier)}" +
                  "&code_challenge_method=S256";
        if (linkCode != null)
            url += $"&link_code={Uri.EscapeDataString(linkCode)}";
        return url;
    }

    private async Task<string> GetLinkCodeAsync(CancellationToken ct = default)
    {
        await RefreshTokenAsync(false, ct);
        var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/auth/linkCode")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", Credentials.AccessToken) }
        };
        var resp = await httpClient.SendAsync(request, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    public async Task<UserCredentials> GetTokenAsync(string code, CancellationToken ct = default)
    {
        var verifier = _codeVerifier;
        var redirectUrl = _pendingRedirectUrl;
        _codeVerifier = null;
        _pendingState = null;
        _pendingRedirectUrl = null;

        if (string.IsNullOrEmpty(verifier) || string.IsNullOrEmpty(redirectUrl))
            throw new InvalidOperationException(
                "GetAuthorizationUrl must be called before GetTokenAsync: the PKCE verifier is not available");

        var form = new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "client_id", clientId },
            { "code", code },
            { "redirect_uri", redirectUrl },
            { "code_verifier", verifier },
        };
        if (!string.IsNullOrEmpty(clientSecret))
            form["client_secret"] = clientSecret;

        var resp = await httpClient.PostAsync("api/v1/auth/token", new FormUrlEncodedContent(form), ct);
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<UserCredentialsSchema>(JsonOptions, ct) ??
                   throw new Exception("Invalid response");
        Credentials = UserCredentials.FromSchema(data);
        if (credentialsStore != null)
            await credentialsStore.SaveCredentials(Credentials, ct);
        return Credentials;
    }

    public async Task<UserCredentials> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var resp = await httpClient.PostAsync("api/v1/auth/token", WithClientSecret(new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "client_id", clientId },
            { "refresh_token", refreshToken },
        }), ct);
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<UserCredentialsSchema>(JsonOptions, ct) ??
                   throw new Exception("Invalid response");
        return UserCredentials.FromSchema(data);
    }

    public async Task<UserCredentials> RefreshTokenAsync(UserCredentials credentials, bool force = false,
        CancellationToken ct = default)
    {
        if (!force && credentials.ExpiresAt - DateTime.UtcNow > TimeSpan.FromMinutes(1))
            return credentials;
        return await RefreshTokenAsync(credentials.RefreshToken, ct);
    }

    private bool _isRefreshing;

    [MemberNotNull(nameof(Credentials))]
    public async Task<UserCredentials> RefreshTokenAsync(bool force = false, CancellationToken ct = default)
    {
        if (_isRefreshing)
        {
            while (_isRefreshing)
                await Task.Delay(500, ct);
            return Credentials ?? throw new Exception("Not authorized");
        }

        _isRefreshing = true;
        if (credentialsStore != null)
            Credentials ??= await credentialsStore.LoadCredentials(ct);
        if (Credentials == null)
        {
            _isRefreshing = false;
            throw new Exception("Not authorized");
        }
        Credentials = await RefreshTokenAsync(Credentials, force, ct);
        if (credentialsStore != null)
            await credentialsStore.SaveCredentials(Credentials, ct);
        _isRefreshing = false;
        return Credentials;
    }

    public async Task RevokeTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var resp = await httpClient.PostAsync("api/v1/auth/revoke", WithClientSecret(new Dictionary<string, string>
        {
            { "client_id", clientId },
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
        if (credentialsStore != null)
            await credentialsStore.SaveCredentials(null, ct);
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
        var data = await resp.Content.ReadFromJsonAsync<UserInfo>(JsonOptions, ct) ??
                   throw new Exception("Invalid response");
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
        var data = await resp.Content.ReadFromJsonAsync<AccountCredentials>(JsonOptions, ct) ??
                   throw new Exception("Invalid response");
        return data;
    }

    public async Task<UserCredentials> AuthorizeInstalledAsync(string provider, string redirectUrl,
        CancellationToken ct = default)
    {
        var url = GetAuthorizationUrl(provider, redirectUrl);
        var code = await OpenAndReceiveCodeAsync(url, redirectUrl, ct);
        return await GetTokenAsync(code, ct);
    }

    public async Task LinkInstalledAsync(string provider, string redirectUrl, CancellationToken ct = default)
    {
        await RefreshTokenAsync(false, ct);
        var url = await GetLinkUrl(provider, redirectUrl, ct);
        // The link flow does not exchange the code, but the redirect must still carry our state.
        await OpenAndReceiveCodeAsync(url, redirectUrl, ct);
        _codeVerifier = null;
        _pendingState = null;
        _pendingRedirectUrl = null;
    }

    private async Task<string> OpenAndReceiveCodeAsync(string url, string redirectUrl, CancellationToken ct)
    {
        OpenUrl(url);
        var result = await ReceiveAuthCodeAsync(redirectUrl, _pendingState, ct);
        if (result.Code == null)
            throw new Exception("Authorization failed");
        if (!string.Equals(result.State, _pendingState, StringComparison.Ordinal))
            throw new Exception("Authorization state mismatch");
        return result.Code;
    }

    private FormUrlEncodedContent WithClientSecret(Dictionary<string, string> form)
    {
        if (!string.IsNullOrEmpty(clientSecret))
            form["client_secret"] = clientSecret;
        return new FormUrlEncodedContent(form);
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

    private static async Task<(string? Code, string? State)> ReceiveAuthCodeAsync(string redirectUrl,
        string? expectedState, CancellationToken ct)
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
        var code = request.QueryString.Get("code");
        var state = request.QueryString.Get("state");

        // A redirect with a wrong/missing state is not ours: do not hand the code to the caller.
        var stateOk = expectedState == null || string.Equals(state, expectedState, StringComparison.Ordinal);
        if (!stateOk)
            code = null;

        // Отправляем ответ клиенту
        var response = context.Response;
        var bytes = Encoding.UTF8.GetBytes(stateOk
            ? "Code received. Close this page"
            : "Authorization failed. Close this page");
        response.StatusCode = stateOk ? 200 : 400;
        response.ContentLength64 = bytes.Length;
        response.ContentEncoding = Encoding.UTF8;
        await using (var output = response.OutputStream)
        {
            await output.WriteAsync(bytes, ct);
            output.Close();
        }

        listener.Stop();

        return (code, state);
    }
}