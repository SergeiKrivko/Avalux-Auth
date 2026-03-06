using AvaluxAuth.Abstractions;
using AvaluxAuth.Api.Schemas;
using AvaluxAuth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("api/v1/admin/apps/{applicationId:guid}/users")]
[Authorize(Policy = Config.AdminPolicy)]
[EnableCors(PolicyName = Config.AdminPolicy)]
public class UsersController(
    IUserRepository userRepository,
    IUserService userService,
    ISubscriptionRepository subscriptionRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UsersResponseSchema>> GetUsers(Guid applicationId,
        [FromQuery] int page = 0,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
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
        var user = await userRepository.GetUserWithAccountsAsync(userId, ct);
        if (user == null)
            return NotFound();
        return Ok(user);
    }

    [HttpGet("{userId:guid}/accessToken")]
    public async Task<ActionResult<string>> GetUserAccessToken(Guid applicationId, Guid userId,
        [FromQuery] Guid? providerId, [FromQuery] string? providerKey,
        CancellationToken ct)
    {
        if (providerId == null && providerKey == null)
            return BadRequest("providerId or providerKey is required");
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
        var res = await userRepository.DeleteUserAsync(userId, ct);
        if (!res)
            return NotFound("User not found");
        return Ok("User successfully deleted");
    }

    [HttpPost("{userId:guid}/subscriptions")]
    public async Task<ActionResult> AddUserSubscription(Guid applicationId, Guid userId,
        AddSubscriptionRequestSchema schema, CancellationToken ct)
    {
        await subscriptionRepository.AddUserSubscriptionAsync(userId, schema.PlanId, schema.ExpiresAt, ct);
        return Ok();
    }
}