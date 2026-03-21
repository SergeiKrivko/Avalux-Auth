using System.Security.Cryptography;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Api.Schemas;
using AvaluxAuth.Models;
using AvaluxAuth.Utils;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("api/v1/password")]
[EnableCors(PolicyName = Config.AdminPolicy)]
public class PasswordController(
    IStateRepository stateRepository,
    IAuthCodeRepository codeRepository,
    IApplicationRepository applicationRepository,
    IPasswordService passwordService) : ControllerBase
{
    [HttpPost("signup")]
    public async Task<ActionResult<PasswordAuthResponseSchema>> PasswordSignUp([FromQuery] string state,
        [FromBody] PasswordSignUpSchema schema,
        CancellationToken ct = default)
    {
        try
        {
            var stateData = await stateRepository.GetStateAsync(state);
            if (stateData == null)
                return Unauthorized();

            if (await passwordService.CheckUserExistsAsync(schema.Login, ct))
                return Conflict("Account already exists");
            var password = await passwordService.CreateUserAsync(schema.Login, schema.Password, schema.UserInfo, ct);
            var userId =
                await passwordService.GetOrCreateAccountAsync(stateData.ApplicationId, stateData.LinkUserId, password,
                    ct);

            return Ok(await CreateResponseSchemaAsync(stateData, userId));
        }
        catch (Exception e)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
        }
    }

    [HttpPost("signin")]
    public async Task<ActionResult<PasswordAuthResponseSchema>> PasswordSignIn([FromQuery] string state,
        [FromBody] PasswordSignInSchema schema,
        CancellationToken ct = default)
    {
        try
        {
            var stateData = await stateRepository.GetStateAsync(state);
            if (stateData == null)
                return Unauthorized();

            var password = await passwordService.VerifyPasswordAsync(schema.Login, schema.Password, ct);
            if (password == null)
                return Unauthorized("Wrong login or password");
            var userId =
                await passwordService.GetOrCreateAccountAsync(stateData.ApplicationId, stateData.LinkUserId, password,
                    ct);

            return Ok(await CreateResponseSchemaAsync(stateData, userId));
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
                await passwordService.ChangePasswordAsync(user, schema.OldPassword, schema.NewPassword, ct);

            var res = await passwordService.UpdateInfoAsync(user.Id, stateData.LinkUserId.Value,
                stateData.ApplicationId,
                schema.UserInfo, ct);
            if (!res)
                return NotFound("Profile not found");

            return Ok(await CreateResponseSchemaAsync(stateData, stateData.LinkUserId.Value));
        }
        catch (Exception e)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
        }
    }

    private async Task<PasswordAuthResponseSchema> CreateResponseSchemaAsync(AuthorizationState stateData, Guid userId)
    {
        await stateRepository.DeleteStateAsync(stateData.State);

        var code = RandomNumberGenerator.GetRandomString();
        await codeRepository.SaveCodeAsync(new AuthCode
        {
            Code = code,
            AuthTime = DateTimeOffset.UtcNow,
            UserId = userId,
            UserNonce = stateData.UserNonce,
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