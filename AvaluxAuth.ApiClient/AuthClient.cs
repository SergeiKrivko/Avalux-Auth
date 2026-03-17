using System.Net.Http.Headers;
using System.Net.Http.Json;
using Avalux.Auth.ApiClient.Models;

namespace Avalux.Auth.ApiClient;

public class AuthClient(HttpClient httpClient) : IAuthClient
{
    public AuthClient(string url, string serviceAccountToken) : this(new HttpClient
    {
        BaseAddress = new Uri(url),
        DefaultRequestHeaders = { Authorization = new AuthenticationHeaderValue("Bearer", serviceAccountToken) }
    })
    {
    }

    public async Task<IEnumerable<UserInfo>> GetUsersAsync(int page = 0, int limit = 100,
        CancellationToken ct = default)
    {
        var resp = await httpClient.GetAsync($"api/v1/service/users?page={page}&limit={limit}", ct);
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<UserInfo[]>(ct);
        return data ?? throw new Exception("Invalid response");
    }

    public async Task<IEnumerable<UserInfo>> SearchUsersAsync(int page = 0, int limit = 100, string? login = null,
        string? email = null,
        string? provider = null, CancellationToken ct = default)
    {
        var url = $"api/v1/service/users?page={page}&limit={limit}";
        if (login != null)
            url += $"&login={login}";
        if (email != null)
            url += $"&email={email}";
        if (provider != null)
            url += $"&provider={provider}";

        var resp = await httpClient.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<UserInfo[]>(ct);
        return data ?? throw new Exception("Invalid response");
    }

    public async Task<UserInfo> GetUserAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await httpClient.GetAsync($"api/v1/service/users/{id}", ct);
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<UserInfo>(ct);
        return data ?? throw new Exception("Invalid response");
    }

    public async Task<AccountCredentials> GetAccessTokenAsync(Guid id, string provider, CancellationToken ct = default)
    {
        var resp = await httpClient.GetAsync($"api/v1/service/users/{id}/accessToken", ct);
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<AccountCredentials>(ct);
        return data ?? throw new Exception("Invalid response");
    }

    public async Task DeleteUserAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await httpClient.DeleteAsync($"api/v1/service/users/{id}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<IEnumerable<SubscriptionPlan>> GetSubscriptionPlansAsync(CancellationToken ct = default)
    {
        var resp = await httpClient.GetAsync($"api/v1/service/subscriptions/plans", ct);
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<SubscriptionPlan[]>(ct);
        return data ?? throw new Exception("Invalid response");
    }

    public async Task<Dictionary<string, T>> GetSubscriptionPlansDataAsync<T>(CancellationToken ct = default)
    {
        var resp = await httpClient.GetAsync($"api/v1/service/subscriptions/plans", ct);
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<Dictionary<string, T>>(ct);
        return data ?? throw new Exception("Invalid response");
    }
}