using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Api.Schemas;
using AvaluxAuth.Models;
using AvaluxAuth.Services;
using AvaluxAuth.Utils;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("api/v1/password")]
[EnableCors(PolicyName = Config.AdminPolicy)]
public class PasswordController(
    IStateRepository stateRepository,
    IAuthCodeRepository codeRepository,
    IApplicationRepository applicationRepository,
    IUserRepository userRepository,
    IPasswordService passwordService,
    ILoginThrottle loginThrottle,
    ILogger<PasswordController> logger) : ControllerBase
{
    // Matches the Passwords.Login column length; longer values would only
    // produce a 500 on insert.
    private const int MaxLoginLength = 64;

    [HttpPost("signup")]
    [EnableRateLimiting(Config.AuthRateLimitPolicy)]
    public async Task<ActionResult<PasswordAuthResponseSchema>> PasswordSignUp([FromQuery] string state,
        [FromBody] PasswordSignUpSchema schema,
        CancellationToken ct = default)
    {
        try
        {
            if (!IsValidLogin(schema.Login))
                return BadRequest("Invalid login");

            // Server-side policy (H-06): the auth UI checks the same rules, but
            // direct API calls must not bypass them.
            var policyError = PasswordPolicy.Validate(schema.Password);
            if (policyError != null)
                return BadRequest(policyError);

            var stateData = await stateRepository.GetStateAsync(state);
            if (stateData == null || stateData.ClientId == null)
                return Unauthorized();

            var clientIp = HttpContext.GetClientIpAddress();
            var retryAfter = await loginThrottle.GetRetryAfterAsync(Config.PasswordSignUpScope, clientIp,
                schema.Login, ct);
            if (retryAfter != null)
                return TooManyRequests(retryAfter.Value);

            if (await passwordService.CheckUserExistsAsync(schema.Login, ct))
            {
                // H-06: "Account already exists" stays an existence oracle by
                // design (there is no e-mail confirmation flow), so the failure
                // is throttled and observed like a failed sign-in.
                var lockout = await RegisterFailureAsync(state, clientIp, Config.PasswordSignUpScope,
                    schema.Login, ct);
                if (lockout != null)
                    return TooManyRequests(lockout.Value);
                return Conflict("Account already exists");
            }

            var password = await passwordService.CreateUserAsync(schema.Login, schema.Password, schema.UserInfo, ct);
            var userId =
                await passwordService.GetOrCreateAccountAsync(stateData.ApplicationId, stateData.LinkUserId, password,
                    ct);
            await loginThrottle.ResetAsync(Config.PasswordSignUpScope, clientIp, schema.Login, ct);

            return Ok(await CreateResponseSchemaAsync(stateData, userId, ct));
        }
        catch (Exception e)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
        }
    }

    [HttpPost("signin")]
    [EnableRateLimiting(Config.AuthRateLimitPolicy)]
    public async Task<ActionResult<PasswordAuthResponseSchema>> PasswordSignIn([FromQuery] string state,
        [FromBody] PasswordSignInSchema schema,
        CancellationToken ct = default)
    {
        try
        {
            var stateData = await stateRepository.GetStateAsync(state);
            if (stateData == null || stateData.ClientId == null)
                return Unauthorized();

            if (!IsValidLogin(schema.Login))
                return Unauthorized("Wrong login or password");

            var clientIp = HttpContext.GetClientIpAddress();
            var retryAfter = await loginThrottle.GetRetryAfterAsync(Config.PasswordSignInScope, clientIp,
                schema.Login, ct);
            if (retryAfter != null)
                return TooManyRequests(retryAfter.Value);

            var password = await passwordService.VerifyPasswordAsync(schema.Login, schema.Password, ct);
            if (password == null)
            {
                var lockout = await RegisterFailureAsync(state, clientIp, Config.PasswordSignInScope,
                    schema.Login, ct);
                if (lockout != null)
                    return TooManyRequests(lockout.Value);
                return Unauthorized("Wrong login or password");
            }

            await loginThrottle.ResetAsync(Config.PasswordSignInScope, clientIp, schema.Login, ct);

            var userId =
                await passwordService.GetOrCreateAccountAsync(stateData.ApplicationId, stateData.LinkUserId, password,
                    ct);

            return Ok(await CreateResponseSchemaAsync(stateData, userId, ct));
        }
        catch (Exception e)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
        }
    }

    [HttpGet("clientInfo")]
    public async Task<ActionResult<ClientInfoResponse>> GetClientName([FromQuery] string state,
        CancellationToken ct = default)
    {
        var stateData = await stateRepository.GetStateAsync(state);
        if (stateData == null)
            return Unauthorized();
        var application = await applicationRepository.GetApplicationByIdAsync(stateData.ApplicationId, ct);
        return Ok(new ClientInfoResponse
        {
            Name = application?.Parameters.Name
        });
    }

    [HttpGet("profile")]
    public async Task<ActionResult<PasswordUserInfo>> GetUserInfo([FromQuery] string state, CancellationToken ct)
    {
        var stateData = await stateRepository.GetStateAsync(state);
        if (stateData == null || stateData.LinkUserId == null)
            return Unauthorized();
        var user = await passwordService.GetByUserId(stateData.LinkUserId.Value, ct);
        return Ok(user?.Info);
    }

    [HttpPut("profile")]
    public async Task<ActionResult<PasswordAuthResponseSchema>> SaveProfile([FromQuery] string state,
        [FromBody] UpdateProfileSchema schema,
        CancellationToken ct)
    {
        try
        {
            var stateData = await stateRepository.GetStateAsync(state);
            if (stateData == null || stateData.LinkUserId == null)
                return Unauthorized();

            var user = await passwordService.GetByUserId(stateData.LinkUserId.Value, ct);
            if (user == null)
                return NotFound("Profile not found");

            if (!string.IsNullOrEmpty(schema.NewPassword) && schema.OldPassword != null)
            {
                var policyError = PasswordPolicy.Validate(schema.NewPassword);
                if (policyError != null)
                    return BadRequest(policyError);

                await passwordService.ChangePasswordAsync(stateData.LinkUserId.Value, user, schema.OldPassword,
                    schema.NewPassword, ct);
            }

            var res = await passwordService.UpdateInfoAsync(user.Id, stateData.LinkUserId.Value,
                stateData.ApplicationId,
                schema.UserInfo, ct);
            if (!res)
                return NotFound("Profile not found");

            return Ok(await CreateResponseSchemaAsync(stateData, stateData.LinkUserId.Value, ct));
        }
        catch (Exception e)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
        }
    }

    /// <summary>
    /// H-06: a failure is counted both per (IP, login) and per authorization
    /// state. After <see cref="Config.MaxFailedAttemptsPerState"/> failures the
    /// state is burned, so one valid state cannot be used for unlimited guesses.
    /// </summary>
    private async Task<TimeSpan?> RegisterFailureAsync(string state, string clientIp, string scope, string login,
        CancellationToken ct)
    {
        var attempts = await stateRepository.RegisterFailureAsync(state);
        var lockout = await loginThrottle.RegisterFailureAsync(scope, clientIp, login, ct);

        // Audit trail for failed sign-ins; the login itself is hashed.
        logger.LogWarning(
            "Password authentication failed: scope={Scope}, ip={ClientIp}, loginHash={LoginHash}, stateAttempts={StateAttempts}",
            scope, clientIp, HashLogin(login), attempts);

        if (attempts >= Config.MaxFailedAttemptsPerState)
        {
            await stateRepository.DeleteStateAsync(state);
            logger.LogWarning(
                "Authorization state invalidated after repeated failures: ip={ClientIp}, stateAttempts={StateAttempts}",
                clientIp, attempts);
        }

        return lockout;
    }

    private ActionResult TooManyRequests(TimeSpan retryAfter)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        return StatusCode(StatusCodes.Status429TooManyRequests, "Too many attempts. Try again later.");
    }

    private static bool IsValidLogin(string? login)
    {
        return !string.IsNullOrWhiteSpace(login) && login.Length <= MaxLoginLength;
    }

    private static string HashLogin(string login)
    {
        var normalized = login.Trim().ToLowerInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..16];
    }

    private async Task<PasswordAuthResponseSchema> CreateResponseSchemaAsync(AuthorizationState stateData, Guid userId,
        CancellationToken ct)
    {
        await stateRepository.DeleteStateAsync(stateData.State);

        var code = RandomNumberGenerator.GetRandomString();
        // M-03: read the generation after any revocation this flow performed, so the
        // code (and the tokens it yields) belongs to the re-issued session.
        var sessionVersion = await userRepository.GetSessionVersionAsync(userId, ct) ?? 0;
        await codeRepository.SaveCodeAsync(new AuthCode
        {
            Code = code,
            AuthTime = DateTimeOffset.UtcNow,
            UserId = userId,
            UserNonce = stateData.UserNonce,
            ClientId = stateData.ClientId,
            RedirectUri = stateData.RedirectUrl,
            CodeChallenge = stateData.CodeChallenge,
            CodeChallengeMethod = stateData.CodeChallengeMethod,
            SessionVersion = sessionVersion,
        });

        var builder = new UrlBuilder(stateData.RedirectUrl)
            .AddQuery("code", code);
        if (stateData.UserState != null)
            builder.AddQuery("state", stateData.UserState);
        return new PasswordAuthResponseSchema
        {
            RedirectUrl = builder.ToString()
        };
    }
}
