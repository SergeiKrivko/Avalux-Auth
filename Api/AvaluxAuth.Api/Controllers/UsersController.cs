using AvaluxAuth.Abstractions;
using AvaluxAuth.Api.Schemas;
using AvaluxAuth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("api/v1/admin/apps/{applicationId:guid}/users")]
[Authorize(Policy = Config.AdminOrServiceAccountPolicy)]
[EnableCors(PolicyName = Config.AdminPolicy)]
public class UsersController(IUserRepository userRepository, IUserService userService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UsersResponseSchema>> GetUsers(Guid applicationId,
        [FromQuery] int page = 0,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        if (!User.HasApplication(applicationId) || !User.HasPermission(TokenPermissions.ReadUserInfo))
            return Unauthorized();
        var users = limit == null
            ? await userRepository.GetUsersAsync(applicationId, ct)
            : await userRepository.GetUsersAsync(applicationId, page, limit.Value, ct);
        var count = await userRepository.CountUsersAsync(applicationId, ct);
        return Ok(new UsersResponseSchema
        {
            Total = count,
            Page = page,
            Limit = limit,
            Users = users,
        });
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<UserWithAccounts>> GetUser(Guid applicationId, Guid userId, CancellationToken ct)
    {
        if (!User.HasApplication(applicationId) || !User.HasPermission(TokenPermissions.ReadUserInfo))
            return Unauthorized();
        var user = await userRepository.GetUserWithAccountsAsync(userId, ct);
        if (user == null)
            return NotFound();
        return Ok(user);
    }

    [HttpGet("{userId:guid}/accessToken")]
    public async Task<ActionResult<UserWithAccounts>> GetUserAccessToken(Guid applicationId, Guid userId,
        [FromQuery] Guid? providerId, [FromQuery] string? providerKey,
        CancellationToken ct)
    {
        if (providerId == null && providerKey == null)
            return BadRequest("providerId or providerKey is required");
        if (!User.HasApplication(applicationId) || !User.HasPermission(TokenPermissions.ReadUserAccessToken))
            return Unauthorized();
        var accessToken = providerId == null
            ? await userService.GetAccessTokenAsync(userId, providerKey!, ct)
            : await userService.GetAccessTokenAsync(userId, providerId.Value, ct);
        if (accessToken == null)
            return NotFound();
        return Ok(accessToken);
    }

    [HttpDelete("{userId:guid}")]
    public async Task<ActionResult> DeleteUser(Guid applicationId, Guid userId, CancellationToken ct = default)
    {
        if (!User.HasApplication(applicationId) || !User.HasPermission(TokenPermissions.DeleteUser))
            return Unauthorized();
        var res = await userRepository.DeleteUserAsync(userId, ct);
        if (!res)
            return NotFound("User not found");
        return Ok("User successfully deleted");
    }
}