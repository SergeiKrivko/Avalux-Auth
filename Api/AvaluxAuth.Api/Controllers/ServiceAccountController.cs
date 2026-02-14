using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("api/v1/service/users")]
[Authorize(Policy = Config.ServiceAccountPolicy)]
[EnableCors(PolicyName = Config.AdminPolicy)]
public class ServiceAccountController(IUserRepository userRepository, IUserService userService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserWithAccounts>>> GetUsers(CancellationToken ct = default)
    {
        if (!User.HasPermission(TokenPermissions.ReadUserInfo))
            return Unauthorized();
        var applicationId = User.ApplicationId;
        var users = await userRepository.GetUsersAsync(applicationId, ct);
        return Ok(users);
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<UserWithAccounts>> GetUser(Guid userId, CancellationToken ct)
    {
        if (!User.HasPermission(TokenPermissions.ReadUserInfo))
            return Unauthorized();
        var user = await userRepository.GetUserWithAccountsAsync(userId, ct);
        if (!User.HasApplication(user?.ApplicationId))
            return Unauthorized();
        if (user == null)
            return NotFound();
        return Ok(user);
    }

    [HttpGet("{userId:guid}/accessToken")]
    public async Task<ActionResult<UserWithAccounts>> GetUserAccessToken(Guid userId,
        [FromQuery] Guid? providerId, [FromQuery] string? providerKey,
        CancellationToken ct)
    {
        if (providerId == null && providerKey == null)
            return BadRequest("providerId or providerKey is required");
        if (!User.HasPermission(TokenPermissions.ReadUserAccessToken))
            return Unauthorized();

        var user = await userRepository.GetUserWithAccountsAsync(userId, ct);
        if (!User.HasApplication(user?.ApplicationId))
            return Unauthorized();

        var accessToken = providerId == null
            ? await userService.GetAccessTokenAsync(userId, providerKey!, ct)
            : await userService.GetAccessTokenAsync(userId, providerId.Value, ct);
        if (accessToken == null)
            return NotFound();
        return Ok(accessToken);
    }

    [HttpDelete("{userId:guid}")]
    public async Task<ActionResult> DeleteUser(Guid userId, CancellationToken ct = default)
    {
        if (!User.HasPermission(TokenPermissions.DeleteUser))
            return Unauthorized();
        var user = await userRepository.GetUserWithAccountsAsync(userId, ct);
        if (!User.HasApplication(user?.ApplicationId))
            return Unauthorized();

        var res = await userRepository.DeleteUserAsync(userId, ct);
        if (!res)
            return NotFound("User not found");
        return Ok("User successfully deleted");
    }
}