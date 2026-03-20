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
        var stateData = await stateRepository.GetStateAsync(state);
        if (stateData == null)
            return Unauthorized();

        if (await passwordService.CheckUserExistsAsync(schema.Login, ct))
            return Conflict("Account already exists");
        var password = await passwordService.CreateUserAsync(schema.Login, schema.Password, schema.UserInfo, ct);
        var userId =
            await passwordService.GetOrCreateAccountAsync(stateData.ApplicationId, stateData.LinkUserId, password, ct);

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
        return Ok(new PasswordAuthResponseSchema
        {
            RedirectUrl = builder.ToString()
        });
    }

    [HttpPost("signin")]
    public async Task<ActionResult<PasswordAuthResponseSchema>> PasswordSignIn([FromQuery] string state,
        [FromBody] PasswordSignInSchema schema,
        CancellationToken ct = default)
    {
        var stateData = await stateRepository.GetStateAsync(state);
        if (stateData == null)
            return Unauthorized();

        var password = await passwordService.VerifyPasswordAsync(schema.Login, schema.Password, ct);
        if (password == null)
            return Unauthorized("Wrong login or password");
        var userId =
            await passwordService.GetOrCreateAccountAsync(stateData.ApplicationId, stateData.LinkUserId, password, ct);

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
        return Ok(new PasswordAuthResponseSchema
        {
            RedirectUrl = builder.ToString()
        });
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
}