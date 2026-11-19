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
    IPasswordRecoveryService passwordRecoveryService,
    IEmailConfirmationService emailConfirmationService,
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

            // R-01: the address is the recovery channel and must fit the columns.
            var emailError = EmailPolicy.Validate(schema.UserInfo.Email);
            if (emailError != null)
                return BadRequest(emailError);

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

            // R-02: confirmation is optional, but when a letter is queued the
            // auth UI shows the "check your e-mail" page with the redirect below
            // as the skip action. The interrupted authorization travels inside
            // the ticket, so following the link signs the user in.
            var confirmation = await emailConfirmationService.StartAsync(password.Id, password.Login,
                password.Info.Email, userId, EmailConfirmationReason.SignUp, stateData, ct);

            return Ok(await CreateResponseSchemaAsync(stateData, userId, confirmation, ct));
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

            return Ok(await CreateResponseSchemaAsync(stateData, userId, null, ct));
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
            Name = application?.Parameters.Name,
            IconUrl = application?.IconUrl
        });
    }

    [HttpGet("profile")]
    public async Task<ActionResult<PasswordProfileSchema?>> GetUserInfo([FromQuery] string state,
        CancellationToken ct)
    {
        var stateData = await stateRepository.GetStateAsync(state);
        if (stateData == null || stateData.LinkUserId == null)
            return Unauthorized();
        var user = await passwordService.GetByUserId(stateData.LinkUserId.Value, ct);
        if (user == null)
            return Ok(null);
        return Ok(new PasswordProfileSchema
        {
            Name = user.Info.Name,
            Email = user.Info.Email,
            AvatarId = user.Info.AvatarId,
            // R-02: the flag belongs to the stored address, not to the account.
            EmailConfirmed = user.EmailConfirmedAt != null,
        });
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

            var emailError = EmailPolicy.Validate(schema.UserInfo.Email);
            if (emailError != null)
                return BadRequest(emailError);

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

            // R-02: a replaced address is unconfirmed again. The user is already
            // signed in here, so the letter only confirms the new mailbox and
            // carries no authorization to finish. The address comes from the
            // request: the scoped DbContext still tracks the pre-update row.
            var emailChanged = !string.Equals(user.Info.Email?.Trim(), schema.UserInfo.Email?.Trim(),
                StringComparison.OrdinalIgnoreCase);
            if (emailChanged && !string.IsNullOrWhiteSpace(schema.UserInfo.Email))
                await emailConfirmationService.StartAsync(user.Id, user.Login, schema.UserInfo.Email,
                    stateData.LinkUserId.Value, EmailConfirmationReason.EmailChanged, null, ct);

            return Ok(await CreateResponseSchemaAsync(stateData, stateData.LinkUserId.Value, null, ct));
        }
        catch (Exception e)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
        }
    }

    /// <summary>
    /// R-01: starts a self-service password reset. The response is deliberately
    /// identical for every account state (existing, missing, deleted, without an
    /// e-mail, rate-limited), so the endpoint cannot be used as an existence
    /// oracle. Delivery is queued and happens after the response.
    /// </summary>
    [HttpPost("recover")]
    [EnableRateLimiting(Config.PasswordRecoverRateLimitPolicy)]
    public async Task<IActionResult> Recover([FromBody] PasswordRecoverSchema schema,
        CancellationToken ct = default)
    {
        Response.Headers.CacheControl = "no-store";

        var identifier = schema.LoginOrEmail.Trim();
        if (identifier.Length == 0 || identifier.Length > Config.MaxRecoveryIdentifierLength)
            return BadRequest("Invalid login or email");

        var result = await passwordRecoveryService.RequestAsync(identifier, HttpContext.GetClientIpAddress(), ct);
        if (result == RecoveryRequestResult.Disabled)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Password recovery is not available");

        return NoContent();
    }

    /// <summary>
    /// R-01: redeems a single-use reset token, replaces the password and revokes
    /// every session linked to the credential. The token is validated before it
    /// is consumed, so a rejected password does not burn the link.
    /// </summary>
    [HttpPost("recover/complete")]
    [EnableRateLimiting(Config.PasswordResetCompleteRateLimitPolicy)]
    public async Task<IActionResult> CompleteRecovery([FromBody] PasswordResetCompleteSchema schema,
        CancellationToken ct = default)
    {
        Response.Headers.CacheControl = "no-store";

        if (!IsValidResetToken(schema.Token))
            return BadRequest("Invalid or expired reset link");

        try
        {
            var outcome = await passwordRecoveryService.CompleteAsync(schema.Token, schema.Password,
                HttpContext.GetClientIpAddress(), ct);
            return outcome.Result switch
            {
                RecoveryCompleteResult.Success => NoContent(),
                RecoveryCompleteResult.PolicyViolation => UnprocessableEntity(
                    outcome.Error ?? "Password does not meet the requirements"),
                _ => BadRequest("Invalid or expired reset link"),
            };
        }
        catch (Exception e)
        {
            // Storage failure (Redis/Postgres). No internals in the response.
            logger.LogError(e, "Password reset failed: ip={ClientIp}", HttpContext.GetClientIpAddress());
            return StatusCode(StatusCodes.Status500InternalServerError, "Password reset failed");
        }
    }

    /// <summary>
    /// R-02: redeems a single-use confirmation link. The address is marked as
    /// confirmed and, when the ticket carries the authorization request that
    /// registration interrupted, a fresh code is issued and returned as the
    /// redirect URL, so the user is not asked for credentials again. The page
    /// GET never redeems the token: it arrives in the URL fragment and is sent
    /// here only in the body.
    /// </summary>
    [HttpPost("confirm")]
    [EnableRateLimiting(Config.EmailConfirmRateLimitPolicy)]
    public async Task<ActionResult<EmailConfirmationResponseSchema>> ConfirmEmail(
        [FromBody] EmailConfirmationCompleteSchema schema,
        CancellationToken ct = default)
    {
        Response.Headers.CacheControl = "no-store";

        if (!IsValidConfirmationToken(schema.Token))
            return BadRequest("Invalid or expired confirmation link");

        try
        {
            var outcome = await emailConfirmationService.ConfirmAsync(schema.Token,
                HttpContext.GetClientIpAddress(), ct);
            if (outcome.Result != EmailConfirmationResult.Success)
                return BadRequest("Invalid or expired confirmation link");

            return Ok(new EmailConfirmationResponseSchema { RedirectUrl = outcome.RedirectUrl });
        }
        catch (Exception e)
        {
            // Storage failure (Redis/Postgres). No internals in the response.
            logger.LogError(e, "E-mail confirmation failed: ip={ClientIp}", HttpContext.GetClientIpAddress());
            return StatusCode(StatusCodes.Status500InternalServerError, "E-mail confirmation failed");
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

    // R-01: tokens are 32 random bytes encoded as lowercase hex. Anything else
    // is rejected before Redis is queried.
    private static bool IsValidResetToken(string? token)
    {
        return token is { Length: Config.ResetTokenLength } && token.All(char.IsAsciiHexDigitLower);
    }

    // R-02: the confirmation token has the same shape as a reset token.
    private static bool IsValidConfirmationToken(string? token)
    {
        return token is { Length: Config.ConfirmationTokenLength } && token.All(char.IsAsciiHexDigitLower);
    }

    private static string HashLogin(string login)
    {
        var normalized = login.Trim().ToLowerInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..16];
    }

    private async Task<PasswordAuthResponseSchema> CreateResponseSchemaAsync(AuthorizationState stateData, Guid userId,
        EmailConfirmationStarted? confirmation, CancellationToken ct)
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
            RedirectUrl = builder.ToString(),
            EmailConfirmation = confirmation == null
                ? null
                : new EmailConfirmationSchema
                {
                    Email = confirmation.Email,
                    ExpiresAt = confirmation.ExpiresAt,
                },
        };
    }
}
