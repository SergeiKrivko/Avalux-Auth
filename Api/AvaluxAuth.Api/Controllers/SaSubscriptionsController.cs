using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("api/v1/service/subscriptions")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = Config.ServiceAccountPolicy)]
[EnableCors(PolicyName = Config.AdminPolicy)]
public class SaSubscriptionsController(ISubscriptionRepository subscriptionRepository) : ControllerBase
{
    [HttpGet("plans")]
    public async Task<ActionResult<IEnumerable<SubscriptionPlan>>> GetPlans(CancellationToken ct)
    {
        if (!User.HasPermission(TokenPermission.ReadSubscriptionPlans))
            return Unauthorized();
        var applicationId = User.ApplicationId;
        var result = await subscriptionRepository.GetAllPlansAsync(applicationId, ct);
        return Ok(result);
    }

    [HttpGet("plans/data")]
    public async Task<ActionResult<Dictionary<string, object>>> GetPlansData(CancellationToken ct)
    {
        if (!User.HasPermission(TokenPermission.ReadSubscriptionPlans))
            return Unauthorized();
        var applicationId = User.ApplicationId;
        var result = await subscriptionRepository.GetAllPlansAsync(applicationId, ct);
        return Ok(result.ToDictionary(e => e.Info.Key, e => e.Info.Data));
    }
}