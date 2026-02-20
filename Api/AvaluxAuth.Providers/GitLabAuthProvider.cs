using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Web;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;

namespace AvaluxAuth.Providers;

public class GitLabAuthProvider(IHttpClientFactory clientFactory) : IAuthProvider
{
    private readonly HttpClient _httpClient = clientFactory.CreateClient("gitlab");
    private readonly string _gitLabUrl = "https://gitlab.com";
    private readonly string _apiVersion = DefaultApiVersion;

    // Версия API GitLab по умолчанию
    private const string DefaultApiVersion = "v4";

    public string Name => "GitLab";
    public string Key => "gitlab";
    public int Id => 4;
    public string? ProviderUrl => _gitLabUrl;

    public string GetAuthUrl(ProviderParameters parameters, string redirectUrl, string state)
    {
        if (string.IsNullOrEmpty(parameters.ClientId))
            throw new ArgumentException("ClientId is required", nameof(parameters));

        var queryParams = new Dictionary<string, string>
        {
            ["client_id"] = parameters.ClientId,
            ["redirect_uri"] = redirectUrl,
            ["state"] = state,
            ["response_type"] = "code",
            ["scope"] = FormatScope(parameters.DefaultScope)
        };

        return BuildUrl($"{_gitLabUrl}/oauth/authorize", queryParams);
    }

    public async Task<AccountCredentials> GetTokenAsync(
        ProviderParameters parameters,
        Dictionary<string, string> queryParameters,
        string redirectUrl,
        CancellationToken ct)
    {
        ValidateTokenParameters(parameters);

        // Получаем код авторизации из query параметров
        if (!queryParameters.TryGetValue("code", out var code))
        {
            // Проверяем наличие ошибки
            if (queryParameters.TryGetValue("error", out var error))
            {
                var errorDescription = queryParameters.GetValueOrDefault("error_description", "Unknown error");
                throw new InvalidOperationException($"GitLab OAuth error: {error} - {errorDescription}");
            }

            throw new InvalidOperationException("Authorization code not found in query parameters");
        }

        var requestParams = new Dictionary<string, string>
        {
            ["client_id"] = parameters.ClientId!,
            ["client_secret"] = parameters.ClientSecret!,
            ["redirect_uri"] = redirectUrl,
            ["code"] = code,
            ["grant_type"] = "authorization_code"
        };

        try
        {
            var response = await _httpClient.PostAsync(
                $"{_gitLabUrl}/oauth/token",
                new FormUrlEncodedContent(requestParams),
                ct);

            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"GitLab token request failed: {response.StatusCode} - {responseContent}");
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<GitLabTokenResponse>(cancellationToken: ct);

            if (tokenResponse == null)
                throw new InvalidOperationException("Failed to parse token response");

            if (string.IsNullOrEmpty(tokenResponse.AccessToken))
                throw new InvalidOperationException("Access token not found in response");

            return new AccountCredentials
            {
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
            };
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"HTTP error during token acquisition: {ex.Message}", ex);
        }
    }

    public async Task<AccountCredentials> RefreshTokenAsync(
        ProviderParameters parameters,
        string refreshToken,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(parameters.ClientId))
            throw new ArgumentException("ClientId is required", nameof(parameters));

        if (string.IsNullOrEmpty(parameters.ClientSecret))
            throw new ArgumentException("ClientSecret is required", nameof(parameters));

        if (string.IsNullOrEmpty(refreshToken))
            throw new ArgumentException("Refresh token is required", nameof(refreshToken));

        var requestParams = new Dictionary<string, string>
        {
            ["client_id"] = parameters.ClientId,
            ["client_secret"] = parameters.ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        };

        var response = await _httpClient.PostAsync(
            $"{_gitLabUrl}/oauth/token",
            new FormUrlEncodedContent(requestParams),
            ct);

        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"GitLab token refresh failed: {response.StatusCode} - {responseContent}");
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<GitLabTokenResponse>(cancellationToken: ct);

        if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            throw new InvalidOperationException("Failed to parse refresh token response");

        return new AccountCredentials
        {
            AccessToken = tokenResponse.AccessToken,
            RefreshToken =
                tokenResponse.RefreshToken ?? refreshToken, // Используем старый refresh token, если новый не пришел
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
        };
    }

    public async Task<bool> RevokeTokenAsync(
        ProviderParameters parameters,
        AccountCredentials credentials,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(credentials.AccessToken))
                return false;

            // GitLab использует отдельный endpoint для отзыва токена
            var requestParams = new Dictionary<string, string>
            {
                ["token"] = credentials.AccessToken
            };

            // Добавляем client_id и client_secret если они есть (требуется для некоторых версий GitLab)
            if (!string.IsNullOrEmpty(parameters.ClientId))
                requestParams["client_id"] = parameters.ClientId;

            if (!string.IsNullOrEmpty(parameters.ClientSecret))
                requestParams["client_secret"] = parameters.ClientSecret;

            var response = await _httpClient.PostAsync(
                $"{_gitLabUrl}/oauth/revoke",
                new FormUrlEncodedContent(requestParams),
                ct);

            // GitLab возвращает 200 при успешном отзыве
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<UserInfo> GetUserInfoAsync(
        ProviderParameters parameters,
        AccountCredentials credentials,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(credentials.AccessToken))
            throw new ArgumentException("Access token is required", nameof(credentials));

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_gitLabUrl}/api/{_apiVersion}/user");
            request.Headers.Add("Authorization", $"Bearer {credentials.AccessToken}");

            var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"GitLab API error: {response.StatusCode} - {errorContent}");
            }

            var userResponse = await response.Content.ReadFromJsonAsync<GitLabUserResponse>(cancellationToken: ct);

            if (userResponse == null)
                throw new InvalidOperationException("Failed to parse user info response");

            return new UserInfo
            {
                Id = userResponse.Id.ToString(),
                Name = userResponse.Name,
                Email = userResponse.Email,
                AvatarUrl = userResponse.AvatarUrl
            };
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"HTTP error during user info retrieval: {ex.Message}", ex);
        }
    }

    #region Private Methods

    private void ValidateTokenParameters(ProviderParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.ClientId))
            throw new ArgumentException("ClientId is required", nameof(parameters));

        if (string.IsNullOrEmpty(parameters.ClientSecret))
            throw new ArgumentException("ClientSecret is required", nameof(parameters));
    }

    private string FormatScope(string[] scopes)
    {
        if (scopes.Length == 0)
        {
            // GitLab требует хотя бы один scope, используем базовый
            return "read_user";
        }

        return string.Join(" ", scopes);
    }

    private string BuildUrl(string baseUrl, Dictionary<string, string> queryParams)
    {
        var uriBuilder = new UriBuilder(baseUrl);
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);

        foreach (var param in queryParams)
        {
            query[param.Key] = param.Value;
        }

        uriBuilder.Query = query.ToString();
        return uriBuilder.ToString();
    }

    #endregion

    #region Response Models

    private class GitLabTokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; init; }

        [JsonPropertyName("token_type")] public string? TokenType { get; init; }

        [JsonPropertyName("expires_in")] public int ExpiresIn { get; init; }

        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; init; }

        [JsonPropertyName("scope")] public string? Scope { get; init; }

        [JsonPropertyName("created_at")] public long CreatedAt { get; init; }
    }

    private class GitLabUserResponse
    {
        [JsonPropertyName("id")] public int Id { get; init; }

        [JsonPropertyName("username")] public string? Username { get; init; }

        [JsonPropertyName("name")] public string? Name { get; init; }

        [JsonPropertyName("email")] public string? Email { get; init; }

        [JsonPropertyName("avatar_url")] public string? AvatarUrl { get; init; }

        [JsonPropertyName("web_url")] public string? WebUrl { get; init; }

        [JsonPropertyName("state")] public string? State { get; init; }

        [JsonPropertyName("confirmed_at")] public DateTime? ConfirmedAt { get; init; }

        [JsonPropertyName("last_activity_on")] public string? LastActivityOn { get; init; }
    }

    #endregion
}