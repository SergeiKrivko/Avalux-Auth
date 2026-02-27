using AvaluxAuth.Abstractions;
using AvaluxAuth.Api.Schemas;
using AvaluxAuth.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("api/v1/service/users")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = Config.ServiceAccountPolicy)]
[EnableCors(PolicyName = Config.AdminPolicy)]
public class ServiceAccountController(
    IUserRepository userRepository,
    IUserService userService,
    IProviderRepository providerRepository,
    IProviderFactory providerFactory,
    IEnumerable<IAuthProvider> authProviders) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserInfoResponseSchema>>> GetUsers([FromQuery] string? username = null,
        [FromQuery] string? email = null,
        [FromQuery] string? provider = null,
        [FromQuery] int page = 0,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        if (!User.HasPermission(TokenPermission.SearchUsers))
            return Unauthorized();
        var applicationId = User.ApplicationId;

        Provider? p = null;
        if (provider != null && providerFactory.TryGetProvider(provider, out var authProvider))
        {
            p = await providerRepository.GetProviderByProviderIdAsync(applicationId, authProvider.Id, ct);
        }

        var users = await userRepository.SearchUsersAsync(applicationId, username, email, p?.Id, page, limit,
            ct);
        var lst = new List<UserInfoResponseSchema>();
        foreach (var user in users)
        {
            lst.Add(await ConvertUser(user, ct));
        }
        return Ok(lst);
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<UserInfoResponseSchema>> GetUser(Guid userId, CancellationToken ct)
    {
        if (!User.HasPermission(TokenPermission.ReadUserInfo))
            return Unauthorized();
        var user = await userRepository.GetUserWithAccountsAsync(userId, ct);
        if (!User.HasApplication(user?.ApplicationId))
            return Unauthorized();
        if (user == null)
            return NotFound();
        return Ok(await ConvertUser(user, ct));
    }

    private async Task<UserInfoResponseSchema> ConvertUser(UserWithAccounts user, CancellationToken ct = default)
    {
        var providers = await providerRepository.GetAllProvidersAsync(user.ApplicationId, ct);
        return new UserInfoResponseSchema
        {
            Id = user.Id,
            Accounts = user.Accounts.Select(account => new AccountInfoSchema
            {
                Provider = authProviders.First(p => p.Id == providers.First(x => x.Id == account.ProviderId).ProviderId)
                    .Key,
                Id = account.UserInfo.Id,
                Name = account.UserInfo.Name,
                Email = account.UserInfo.Email,
                AvatarUrl = account.UserInfo.AvatarUrl,
            }).ToArray(),
        };
    }

    [HttpGet("{userId:guid}/accessToken")]
    public async Task<ActionResult<AccountCredentials>> GetUserAccessToken(Guid userId,
        [FromQuery] Guid? providerId, [FromQuery] string? providerKey,
        CancellationToken ct)
    {
        if (providerId == null && providerKey == null)
            return BadRequest("providerId or providerKey is required");
        if (!User.HasPermission(TokenPermission.ReadUserAccessToken))
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
        if (!User.HasPermission(TokenPermission.DeleteUser))
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