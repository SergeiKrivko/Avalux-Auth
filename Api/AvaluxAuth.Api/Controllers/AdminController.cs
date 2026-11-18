using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Api.Schemas;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("/api/v1/admin")]
[EnableCors(PolicyName = Config.AdminPolicy)]
public class AdminController(
    IConfiguration configuration,
    IEnumerable<IAuthProvider> providers,
    ISigningKeyService signingKeyService,
    ILoginThrottle loginThrottle,
    ILogger<AdminController> logger) : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting(Config.AdminLoginRateLimitPolicy)]
    public async Task<ActionResult> Authenticate([FromBody] AdminCredentialsSchema credentials, CancellationToken ct)
    {
        var configuredLogin = configuration["Admin:Login"];
        var configuredPassword = configuration["Admin:Password"];

        // Fail closed (M-11): an unset or empty configured password must not
        // authenticate anybody, not even a caller that sends an empty password.
        if (string.IsNullOrEmpty(configuredLogin) || string.IsNullOrEmpty(configuredPassword))
        {
            logger.LogError("Admin login is disabled: Admin:Login or Admin:Password is not configured");
            return Unauthorized();
        }

        var clientIp = HttpContext.GetClientIpAddress();
        var retryAfter = await loginThrottle.GetRetryAfterAsync(Config.AdminLoginScope, clientIp,
            credentials.Login, ct);
        if (retryAfter != null)
            return TooManyRequests(retryAfter.Value);

        // H-06: compare in constant time so the response neither leaks which
        // field was wrong nor how long the configured secret is.
        var loginMatches = FixedTimeEquals(credentials.Login, configuredLogin);
        var passwordMatches = FixedTimeEquals(credentials.Password, configuredPassword);
        if (!loginMatches || !passwordMatches)
        {
            var lockout = await loginThrottle.RegisterFailureAsync(Config.AdminLoginScope, clientIp,
                credentials.Login, ct);
            logger.LogWarning("Admin sign-in failed: ip={ClientIp}", clientIp);
            if (lockout != null)
                return TooManyRequests(lockout.Value);
            return Unauthorized();
        }

        await loginThrottle.ResetAsync(Config.AdminLoginScope, clientIp, credentials.Login, ct);

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
            Key = p.Key,
            Fields = p.Fields,
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

    private ActionResult TooManyRequests(TimeSpan retryAfter)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        return StatusCode(StatusCodes.Status429TooManyRequests, "Too many attempts. Try again later.");
    }

    private static bool FixedTimeEquals(string? left, string right)
    {
        // Hash first so the comparison always runs over fixed-size values and
        // does not reveal the secret's length.
        var leftHash = SHA256.HashData(Encoding.UTF8.GetBytes(left ?? string.Empty));
        var rightHash = SHA256.HashData(Encoding.UTF8.GetBytes(right));
        return CryptographicOperations.FixedTimeEquals(leftHash, rightHash);
    }
}
