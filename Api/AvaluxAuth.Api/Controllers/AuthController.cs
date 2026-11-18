using System.Text.Encodings.Web;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Api.Schemas;
using AvaluxAuth.Models;
using AvaluxAuth.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IAuthorizationService = AvaluxAuth.Abstractions.IAuthorizationService;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(
    IOauthService oauthService,
    IUserRepository userRepository,
    IUserService userService,
    IProviderRepository providerRepository,
    IEnumerable<IAuthProvider> authProviders,
    IAuthorizationService authorizationService,
    IConfiguration configuration,
    ILogger<AuthController> logger)
    : ControllerBase
{
    [HttpGet("authorize")]
    public async Task<ActionResult> Authorize(
        [FromQuery(Name = "provider")] string providerKey,
        [FromQuery(Name = "client_id")] string clientId,
        [FromQuery(Name = "redirect_uri")] string redirectUri,
        [FromQuery(Name = "state")] string? state = null,
        [FromQuery(Name = "nonce")] string? nonce = null,
        [FromQuery(Name = "link_code")] string? linkCode = null,
        CancellationToken ct = default)
    {
        try
        {
            var url = await oauthService.GetAuthUrlAsync(clientId, providerKey, redirectUri, state, nonce, linkCode,
                ct);
            return Redirect(url);
        }
        catch (Exception e)
        {
            return ErrorHtml(e);
        }
    }

    [HttpGet("{providerKey}/callback")]
    public async Task<ActionResult> Callback(string providerKey,
        [FromQuery(Name = "state")] string state,
        CancellationToken ct = default)
    {
        try
        {
            var processed = await oauthService.ProcessCodeAsync(
                Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString()), state, ct);

            var code = await authorizationService.CreateAuthorizationCodeAsync(processed.UserId,
                processed.State.UserNonce, ct);

            var builder = new UrlBuilder(processed.State.RedirectUrl)
                .AddQuery("code", code);
            if (processed.State.UserState != null)
                builder.AddQuery("state", processed.State.UserState);
            return Redirect(builder.ToString());
        }
        catch (Exception e)
        {
            return ErrorHtml(e);
        }
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
        if (configuration["Security:RequireClientSecret"] != null &&
            !await oauthService.CheckClientSecretAsync(clientId, clientSecret, ct))
            return Unauthorized("Client secret is incorrect");

        UserCredentials? credentials;
        switch (grantType)
        {
            case "authorization_code":
                if (code == null)
                    return BadRequest("Parameter 'code' not specified");
                credentials = await authorizationService.GetTokenAsync(code, ct);
                return Ok(credentials);
            case "refresh_token":
                if (refreshToken == null)
                    return BadRequest("Parameter 'refresh_token' not specified");
                credentials = await authorizationService.RefreshTokenAsync(refreshToken, ct);
                if (credentials == null)
                    return Unauthorized("Refresh token is incorrect");
                return Ok(credentials);
            default:
                throw new Exception("invalid grant type");
        }
    }

    [HttpGet("linkCode")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = Config.UserPolicy)]
    public async Task<ActionResult<string>> GetLinkCode(CancellationToken ct = default)
    {
        if (!Guid.TryParse(User.FindFirst("UserId")?.Value, out var userId))
            return Unauthorized();

        var linkCode = await oauthService.CreateLinkCode(userId, ct);
        return Ok(linkCode);
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

    /// <summary>
    /// Renders a generic error page for the browser redirect flows (authorize/callback).
    /// </summary>
    /// <remarks>
    /// The page is deliberately static: exception messages, inner exceptions and query
    /// values (e.g. the provider's <c>error_description</c>) are only written to the log,
    /// never to the markup. The auth origin also hosts the password form, so reflecting
    /// attacker-controlled text here would be a same-origin XSS (H-02 in SECURITY-AUDIT.md).
    /// </remarks>
    private ContentResult ErrorHtml(Exception ex)
    {
        var traceId = HttpContext.TraceIdentifier;
        logger.LogWarning(ex, "Authorization failed: traceId={TraceId}, path={Path}", traceId, Request.Path);

        Response.Headers["Content-Security-Policy"] =
            "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; " +
            "form-action 'none'; frame-ancestors 'none'";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["Cache-Control"] = "no-store";

        var errorHtml = $$"""
            <!DOCTYPE html>
            <html lang="ru">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Ошибка авторизации</title>
                <style>
                    body {
                        font-family: Arial, sans-serif;
                        margin: 40px;
                        line-height: 1.6;
                    }
                    .error-container {
                        max-width: 600px;
                        margin: 0 auto;
                        padding: 20px;
                        border: 1px solid #f5c6cb;
                        border-radius: 5px;
                        background-color: #f8d7da;
                        color: #721c24;
                    }
                    .error-title {
                        font-size: 1.5em;
                        font-weight: bold;
                        margin-bottom: 10px;
                    }
                    .error-message {
                        margin-bottom: 15px;
                    }
                    .error-details {
                        font-size: 0.9em;
                        color: #6c757d;
                        border-top: 1px solid #f5c6cb;
                        padding-top: 10px;
                        margin-top: 10px;
                    }
                </style>
            </head>
            <body>
                <div class="error-container">
                    <div class="error-title">Ошибка авторизации</div>
                    <div class="error-message">
                        Не удалось завершить авторизацию. Вернитесь на страницу входа и попробуйте снова.
                    </div>
                    <div class="error-details">Код ошибки: {{HtmlEncoder.Default.Encode(traceId)}}</div>
                </div>
            </body>
            </html>
            """;

        return new ContentResult
        {
            Content = errorHtml,
            ContentType = "text/html; charset=utf-8",
            StatusCode = StatusCodes.Status400BadRequest,
        };
    }
}