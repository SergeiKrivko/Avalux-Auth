using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Web;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;

namespace AvaluxAuth.Providers;

public class MicrosoftAuthProvider(IHttpClientFactory httpClientFactory) : IAuthProvider
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("microsoft");

    // Базовые URL для Microsoft Identity Platform
    private const string MicrosoftAuthority = "https://login.microsoftonline.com";
    private const string MicrosoftGraphApi = "https://graph.microsoft.com/v1.0";
    private const string DefaultApiVersion = "v2.0";

    public string Name => "Microsoft";
    public string Key => "microsoft";
    public int Id => 5;
    public string ProviderUrl => "https://www.microsoft.com";

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
            ["response_mode"] = "query",
            ["scope"] = FormatScope(parameters.DefaultScope),
            ["prompt"] = "select_account" // Всегда показываем выбор аккаунта
        };

        // Добавляем nonce для безопасности (защита от replay атак)
        var nonce = GenerateNonce();
        queryParams["nonce"] = nonce;

        var authority = BuildAuthorityUrl();
        return BuildUrl($"{authority}/oauth2/{DefaultApiVersion}/authorize", queryParams);
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
                throw new InvalidOperationException($"Microsoft OAuth error: {error} - {errorDescription}");
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
            var authority = BuildAuthorityUrl();
            var response = await _httpClient.PostAsync(
                $"{authority}/oauth2/{DefaultApiVersion}/token",
                new FormUrlEncodedContent(requestParams),
                ct);

            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Microsoft token request failed: {response.StatusCode} - {responseContent}");
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<MicrosoftTokenResponse>(cancellationToken: ct);

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
            ["grant_type"] = "refresh_token",
            ["scope"] = FormatScope(parameters.DefaultScope)
        };

        var authority = BuildAuthorityUrl();
        var response = await _httpClient.PostAsync(
            $"{authority}/oauth2/{DefaultApiVersion}/token",
            new FormUrlEncodedContent(requestParams),
            ct);

        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Microsoft token refresh failed: {response.StatusCode} - {responseContent}");
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<MicrosoftTokenResponse>(cancellationToken: ct);

        if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            throw new InvalidOperationException("Failed to parse refresh token response");

        return new AccountCredentials
        {
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken ?? refreshToken,
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

            // Microsoft поддерживает отзыв токенов через отдельный endpoint
            var requestParams = new Dictionary<string, string>
            {
                ["token"] = credentials.AccessToken,
                ["client_id"] = parameters.ClientId!,
                ["client_secret"] = parameters.ClientSecret!
            };

            var authority = BuildAuthorityUrl();
            var response = await _httpClient.PostAsync(
                $"{authority}/oauth2/{DefaultApiVersion}/logout",
                new FormUrlEncodedContent(requestParams),
                ct);

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

        Console.WriteLine(await GetUserPhotoAsync(credentials.AccessToken, ct));

        try
        {
            // Используем Microsoft Graph API для получения информации о пользователе
            var request = new HttpRequestMessage(HttpMethod.Get, $"{MicrosoftGraphApi}/me");
            request.Headers.Add("Authorization", $"Bearer {credentials.AccessToken}");

            var response = await _httpClient.SendAsync(request, ct);

            response.EnsureSuccessStatusCode();
            var userResponse = await response.Content.ReadFromJsonAsync<MicrosoftUserResponse>(cancellationToken: ct);

            if (userResponse == null)
                throw new InvalidOperationException("Failed to parse user info response");

            return new UserInfo
            {
                Id = userResponse.Id,
                Name = userResponse.DisplayName ?? $"{userResponse.GivenName} {userResponse.Surname}".Trim(),
                Email = userResponse.Mail ?? userResponse.UserPrincipalName,
                AvatarUrl = await GetUserPhotoAsync(credentials.AccessToken, ct)
            };
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"HTTP error during user info retrieval: {ex.Message}", ex);
        }
    }

    #region Private Methods

    /// <summary>
    /// Получение фотографии пользователя из Microsoft Graph
    /// </summary>
    private async Task<string?> GetUserPhotoAsync(string accessToken, CancellationToken ct = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{MicrosoftGraphApi}/me/photo/$value");
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

            var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            var photoBytes = await response.Content.ReadAsByteArrayAsync(ct);
            return $"data:image/jpeg;base64,{Convert.ToBase64String(photoBytes)}";
        }
        catch
        {
            return null;
        }
    }

    private void ValidateTokenParameters(ProviderParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.ClientId))
            throw new ArgumentException("ClientId is required", nameof(parameters));

        if (string.IsNullOrEmpty(parameters.ClientSecret))
            throw new ArgumentException("ClientSecret is required", nameof(parameters));
    }

    private string FormatScope(string[] scopes)
    {
        var defaultScopes = new List<string> { "openid", "profile", "email", "User.Read" };

        if (scopes.Length == 0)
        {
            return string.Join(" ", defaultScopes);
        }

        // Добавляем базовые scopes если их нет
        var allScopes = new HashSet<string>(scopes);
        foreach (var defaultScope in defaultScopes)
        {
            allScopes.Add(defaultScope);
        }

        return string.Join(" ", allScopes);
    }

    private string BuildAuthorityUrl()
    {
        return $"{MicrosoftAuthority}/consumers";
        // return _tenant switch
        // {
        //     "common" => $"{MicrosoftAuthority}/common",
        //     "organizations" => $"{MicrosoftAuthority}/organizations",
        //     "consumers" => $"{MicrosoftAuthority}/consumers",
        //     _ => $"{MicrosoftAuthority}/{_tenant}"
        // };
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

    private string GenerateNonce()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("=", "")
            .Replace("+", "-")
            .Replace("/", "_");
    }

    #endregion

    #region Response Models

    private class MicrosoftTokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; init; }

        [JsonPropertyName("id_token")] public string? IdToken { get; init; }

        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; init; }

        [JsonPropertyName("token_type")] public string? TokenType { get; init; }

        [JsonPropertyName("expires_in")] public int ExpiresIn { get; init; }

        [JsonPropertyName("scope")] public string? Scope { get; init; }
    }

    private class MicrosoftUserResponse
    {
        [JsonPropertyName("id")] public required string Id { get; init; }

        [JsonPropertyName("displayName")] public string? DisplayName { get; init; }

        [JsonPropertyName("givenName")] public string? GivenName { get; init; }

        [JsonPropertyName("surname")] public string? Surname { get; init; }

        [JsonPropertyName("mail")] public string? Mail { get; init; }

        [JsonPropertyName("userPrincipalName")]
        public string? UserPrincipalName { get; init; }
    }

    #endregion
}