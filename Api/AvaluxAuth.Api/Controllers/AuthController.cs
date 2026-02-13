using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using Microsoft.AspNetCore.Mvc;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(IAuthorizationService authorizationService, IAuthCodeRepository authCodeRepository)
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
            authorizationService.ExchangeCredentialsAsync(
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

        var c = await authCodeRepository.TakeCodeAsync(code);
        var credentials = await authorizationService.AuthorizeUserAsync(c.UserId, ct);
        return Ok(credentials);
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
}