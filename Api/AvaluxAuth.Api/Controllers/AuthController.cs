using AvaluxAuth.Abstractions;
using AvaluxAuth.Api.Schemas;
using AvaluxAuth.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IAuthorizationService = AvaluxAuth.Abstractions.IAuthorizationService;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(
    IAuthorizationService authorizationService,
    IUserRepository userRepository,
    IUserService userService,
    IProviderRepository providerRepository,
    IEnumerable<IAuthProvider> authProviders,
    IConfiguration configuration)
    : ControllerBase
{
    [HttpGet("{providerKey}/authorize")]
    public async Task<ActionResult> Authorize(string providerKey,
        [FromQuery(Name = "client_id")] string clientId,
        [FromQuery(Name = "redirect_uri")] string redirectUri,
        CancellationToken ct = default)
    {
        var url = await authorizationService.GetAuthUrlAsync(clientId, providerKey, redirectUri, ct);
        return Redirect(url);
    }

    [HttpGet("authorize")]
    public async Task<ActionResult> AuthorizeFromQuery(
        [FromQuery(Name = "provider")] string providerKey,
        [FromQuery(Name = "client_id")] string clientId,
        [FromQuery(Name = "redirect_uri")] string redirectUri,
        CancellationToken ct = default)
    {
        try
        {
            var url = await authorizationService.GetAuthUrlAsync(clientId, providerKey, redirectUri, ct);
            return Redirect(url);
        }
        catch (Exception ex)
        {
            var errorHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Ошибка авторизации</title>
    <style>
        body {{
            font-family: Arial, sans-serif;
            margin: 40px;
            line-height: 1.6;
        }}
        .error-container {{
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            border: 1px solid #f5c6cb;
            border-radius: 5px;
            background-color: #f8d7da;
            color: #721c24;
        }}
        .error-title {{
            font-size: 1.5em;
            font-weight: bold;
            margin-bottom: 10px;
        }}
        .error-message {{
            margin-bottom: 15px;
            word-wrap: break-word;
        }}
        .error-details {{
            font-size: 0.9em;
            color: #6c757d;
            border-top: 1px solid #f5c6cb;
            padding-top: 10px;
            margin-top: 10px;
        }}
    </style>
</head>
<body>
    <div class='error-container'>
        <div class='error-title'>Ошибка авторизации</div>
        <div class='error-message'><strong>{HtmlEncode(ex.Message)}</strong></div>
        {(ex.InnerException != null ? $"<div class='error-details'>Детали: {HtmlEncode(ex.InnerException.Message)}</div>" : "")}
    </div>
</body>
</html>";

            return Content(errorHtml, "text/html; charset=utf-8");
        }
    }

    // Вспомогательный метод для безопасного отображения текста в HTML
    private static string HtmlEncode(string text)
    {
        return System.Net.WebUtility.HtmlEncode(text);
    }

    [HttpGet("{providerKey}/callback")]
    public async Task<ActionResult> Callback(string providerKey,
        [FromQuery(Name = "state")] string state,
        CancellationToken ct = default)
    {
        var redirectUrl = await
            authorizationService.SaveCodeAsync(
                Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString()), state, ct);
        return Redirect(redirectUrl);
    }

    [HttpPost("token")]
    public async Task<ActionResult<UserCredentials>> GetToken(
        [FromForm(Name = "client_id")] string clientId,
        [FromForm(Name = "client_secret")] string clientSecret,
        [FromForm(Name = "code")] string? code = null,
        [FromForm(Name = "refresh_token")] string? refreshToken = null,
        [FromForm(Name = "grant_type")] string grantType = "authorization_code",
        CancellationToken ct = default)
    {
        if (configuration["Security.RequireClientSecret"] != null &&
            !await authorizationService.CheckClientSecretAsync(clientId, clientSecret, ct))
            return Unauthorized("Client secret is incorrect");

        UserCredentials? credentials;
        switch (grantType)
        {
            case "authorization_code":
                if (code == null)
                    return BadRequest("Parameter 'code' not specified");
                credentials = await authorizationService.AuthorizeUserAsync(code, ct);
                return Ok(credentials);
            case "refresh_token":
                if (refreshToken == null)
                    return BadRequest("Parameter 'code' not specified");
                credentials = await authorizationService.RefreshTokenAsync(refreshToken, ct);
                if (credentials == null)
                    return Unauthorized("Refresh token is incorrect");
                return Ok(credentials);
            default:
                throw new Exception("invalid grant type");
        }
    }

    [HttpPost("link")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = Config.UserPolicy)]
    public async Task<ActionResult> LinkAccount(
        [FromForm(Name = "client_id")] string clientId,
        [FromForm(Name = "client_secret")] string clientSecret,
        [FromForm(Name = "code")] string code,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(User.FindFirst("UserId")?.Value, out var userId))
            return Unauthorized();
        if (!await authorizationService.CheckClientSecretAsync(clientId, clientSecret, ct))
            return Unauthorized("Client secret is incorrect");

        await authorizationService.LinkAccountAsync(userId, code, ct);
        return Ok();
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<UserCredentials>> RefreshToken(
        [FromForm(Name = "refresh_token")] string refreshToken,
        CancellationToken ct = default)
    {
        var credentials = await authorizationService.RefreshTokenAsync(refreshToken, ct);
        if (credentials == null)
            return Unauthorized("Refresh token is incorrect");
        return Ok(credentials);
    }

    [HttpPost("revoke")]
    public async Task<ActionResult> Revoke([FromForm(Name = "refresh_token")] string refreshToken,
        CancellationToken ct = default)
    {
        var res = await authorizationService.RevokeTokenAsync(refreshToken, ct);
        if (!res)
            return Unauthorized("Revoke token is incorrect");
        return Ok();
    }

    [HttpGet("userinfo")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = Config.UserPolicy)]
    public async Task<ActionResult<UserInfoResponseSchema>> GetUserInfo(CancellationToken ct = default)
    {
        if (!Guid.TryParse(User.FindFirst("UserId")?.Value, out var userId))
            return Unauthorized();
        var userInfo = await userRepository.GetUserWithAccountsAsync(userId, ct);
        if (userInfo is null)
            return NotFound();
        var providers = await providerRepository.GetAllProvidersAsync(userInfo.ApplicationId, ct);

        return Ok(new UserInfoResponseSchema
        {
            Id = userInfo.Id,
            Accounts = userInfo.Accounts.Select(account => new AccountInfoSchema
            {
                Provider = authProviders.First(p => p.Id == providers.First(x => x.Id == account.ProviderId).ProviderId)
                    .Key,
                Id = account.UserInfo.Id,
                Name = account.UserInfo.Name,
                Login = account.UserInfo.Login,
                Email = account.UserInfo.Email,
                AvatarUrl = account.UserInfo.AvatarUrl,
            }).ToArray(),
        });
    }

    [HttpGet("{providerKey}/accessToken")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = Config.UserPolicy)]
    public async Task<ActionResult<AccountCredentialsSchema>> GetAccessToken(string providerKey,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(User.FindFirst("UserId")?.Value, out var userId))
            return Unauthorized();
        var credentials = await userService.GetAccessTokenAsync(userId, providerKey, ct);
        if (credentials is null)
            return NotFound();

        return Ok(new AccountCredentialsSchema
        {
            AccessToken = credentials.AccessToken ?? throw new Exception("Access token is null"),
            ExpiresAt = credentials.ExpiresAt,
        });
    }
}