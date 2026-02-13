using System.Security.Claims;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Api.Schemas;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("/api/v1/admin")]
[EnableCors(PolicyName = Config.AdminPolicy)]
public class AdminController(
    IConfiguration configuration,
    IEnumerable<IAuthProvider> providers,
    ISigningKeyService signingKeyService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult> Authenticate([FromBody] AdminCredentialsSchema credentials, CancellationToken ct)
    {
        if (credentials.Login != configuration["Admin.Login"] ||
            credentials.Password != configuration["Admin.Password"])
            return Unauthorized();

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, credentials.Login),
                new Claim(ClaimTypes.Role, Config.AdminRole),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme,
            ClaimTypes.Name,
            ClaimTypes.Role);

        var authProperties = new AuthenticationProperties
        {
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7),
            IsPersistent = true,
            AllowRefresh = true,
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            authProperties);

        return Ok();
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return Ok();
    }

    [HttpGet("test")]
    [Authorize(Policy = Config.AdminRole)]
    public ActionResult Test(CancellationToken ct)
    {
        return Ok();
    }

    [HttpGet("providers-info")]
    [Authorize(Policy = Config.AdminRole)]
    public ActionResult<IEnumerable<ProviderInfo>> ProvidersInfo(CancellationToken ct)
    {
        return Ok(providers.Select(p => new ProviderInfo
        {
            Id = p.Id,
            Name = p.Name,
            Url = p.ProviderUrl,
        }));
    }

    [HttpPost("rotate-signing-key")]
    [Authorize(Policy = Config.AdminRole)]
    public async Task<ActionResult> RotateSiningKey(CancellationToken ct)
    {
        await signingKeyService.RotateSigningKeyAsync(ct);
        return Ok();
    }
}