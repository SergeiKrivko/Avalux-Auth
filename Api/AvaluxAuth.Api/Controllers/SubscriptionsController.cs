using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("api/v1/admin/apps/{applicationId:guid}/subscriptions")]
[Authorize(Policy = Config.AdminPolicy)]
[EnableCors(PolicyName = Config.AdminPolicy)]
public class SubscriptionsController(ISubscriptionRepository subscriptionRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SubscriptionPlan>>> GetAllPlans(Guid applicationId, CancellationToken ct)
    {
        var plans = await subscriptionRepository.GetAllPlansAsync(applicationId, ct);
        return Ok(plans);
    }

    [HttpGet("{planId:guid}")]
    public async Task<ActionResult<SubscriptionPlan>> GetPlanById(Guid applicationId, Guid planId,
        CancellationToken ct)
    {
        var plan = await subscriptionRepository.GetPlanByIdAsync(planId, ct);
        if (plan?.ApplicationId != applicationId)
            return NotFound();
        return Ok(plan);
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> CreatePlan(Guid applicationId, SubscriptionPlanInfo info,
        CancellationToken ct)
    {
        var existing = await subscriptionRepository.GetPlanByKeyAsync(applicationId, info.Key, ct);
        if (existing != null)
            return Conflict("Plan with same key already exists");
        var id = await subscriptionRepository.AddPlanAsync(applicationId, info, ct);
        return Ok(id);
    }

    [HttpPut("{planId:guid}")]
    public async Task<ActionResult> UpdatePlan(Guid applicationId, Guid planId, SubscriptionPlanInfo info,
        CancellationToken ct)
    {
        var existing = await subscriptionRepository.GetPlanByKeyAsync(applicationId, info.Key, ct);
        if (existing != null && existing.Id != planId)
            return Conflict("Plan with same key already exists");
        var res = await subscriptionRepository.UpdatePlanAsync(planId, info, ct);
        if (!res)
            return NotFound();
        return Ok();
    }
}