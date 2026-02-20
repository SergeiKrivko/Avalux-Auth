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
    IEnumerable<IAuthProvider> authProviders)
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
        [FromForm(Name = "code")] string code,
        CancellationToken ct = default)
    {
        if (!await authorizationService.CheckClientSecretAsync(clientId, clientSecret, ct))
            return Unauthorized("Client secret is incorrect");

        var credentials = await authorizationService.AuthorizeUserAsync(code, ct);
        return Ok(credentials);
    }

    [HttpPost("link")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = Config.UserPolicy)]
    public async Task<ActionResult<UserCredentials>> LinkAccount(
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
                Email = account.UserInfo.Email,
                AvatarUrl = account.UserInfo.AvatarUrl,
            }).ToArray(),
        });
    }

    [HttpGet("{providerKey}/access-token")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = Config.UserPolicy)]
    public async Task<ActionResult<UserInfoResponseSchema>> GetAccessToken(string providerKey,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(User.FindFirst("UserId")?.Value, out var userId))
            return Unauthorized();
        var credentials = await userService.GetAccessTokenAsync(userId, providerKey, ct);
        if (credentials is null)
            return NotFound();

        return Ok(credentials);
    }
}