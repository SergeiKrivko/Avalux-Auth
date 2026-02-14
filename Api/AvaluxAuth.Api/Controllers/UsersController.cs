using AvaluxAuth.Abstractions;
using AvaluxAuth.Api.Schemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("api/v1/admin/apps/{applicationId:guid}/users")]
[Authorize(Policy = Config.AdminPolicy)]
[EnableCors(PolicyName = Config.AdminPolicy)]
public class UsersController(IUserRepository userRepository) : ControllerBase
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

    [HttpDelete("{userId:guid}")]
    public async Task<ActionResult> DeleteUser(Guid applicationId, Guid userId, CancellationToken ct = default)
    {
        var res = await userRepository.DeleteUserAsync(userId, ct);
        if (!res)
            return NotFound("User not found");
        return Ok("User successfully deleted");
    }
}