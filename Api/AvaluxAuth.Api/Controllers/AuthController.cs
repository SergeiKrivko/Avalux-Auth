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
    IConfiguration configuration)
    : ControllerBase
{
    [HttpGet("{providerKey}/authorize")]
    public async Task<ActionResult> AuthorizeOld(string providerKey,
        [FromQuery(Name = "client_id")] string clientId,
        [FromQuery(Name = "redirect_uri")] string redirectUri,
        CancellationToken ct = default)
    {
        var url = await oauthService.GetAuthUrlAsync(clientId, providerKey, redirectUri, null, null, null, ct);
        return Redirect(url);
    }

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
        if (configuration["Security.RequireClientSecret"] != null &&
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

        await oauthService.CreateLinkCode(userId, ct);
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

    private ContentResult ErrorHtml(params string[] errors)
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
        {string.Join('\n', errors.Select(e => $"<div class='error-message'>{e}</div>"))}
    </div>
</body>
</html>";
        return Content(errorHtml, "text/html; charset=utf-8");
    }

    private ContentResult ErrorHtml(Exception ex)
    {
        return ex.InnerException == null ? ErrorHtml(ex.Message) : ErrorHtml(ex.Message, ex.InnerException.Message);
    }
}