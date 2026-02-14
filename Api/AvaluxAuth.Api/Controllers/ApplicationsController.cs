using AvaluxAuth.Abstractions;
using AvaluxAuth.Api.Schemas;
using AvaluxAuth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("api/v1/admin/apps")]
[EnableCors(PolicyName = Config.AdminPolicy)]
public class ApplicationsController(
    IApplicationRepository applicationRepository,
    IApplicationService applicationService,
    IUserRepository userRepository) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Config.AdminPolicy)]
    public async Task<ActionResult<Guid>> CreateApplication([FromBody] CreateApplicationSchema schema,
        CancellationToken ct)
    {
        var res = await applicationService.CreateApplicationAsync(new ApplicationParameters
        {
            Name = schema.Name,
            RedirectUrls = [],
        }, ct);
        return Ok(res);
    }

    [HttpGet]
    [Authorize(Policy = Config.AdminPolicy)]
    public async Task<ActionResult<IEnumerable<Application>>> GetApplications(CancellationToken ct)
    {
        var res = await applicationRepository.GetAllApplicationsAsync(ct);
        return Ok(res);
    }

    [HttpGet("{applicationId:guid}")]
    [Authorize(Policy = Config.AdminPolicy)]
    public async Task<ActionResult<Application>> GetApplication(Guid applicationId, CancellationToken ct)
    {
        var res = await applicationRepository.GetApplicationByIdAsync(applicationId, ct);
        return Ok(res);
    }

    [HttpPut("{applicationId:guid}")]
    [Authorize(Policy = Config.AdminPolicy)]
    public async Task<ActionResult> UpdateApplication(Guid applicationId, ApplicationParameters parameters,
        CancellationToken ct)
    {
        var res = await applicationRepository.UpdateApplicationAsync(applicationId, parameters, ct);
        if (!res)
            return NotFound();
        return Ok();
    }

    [HttpDelete("{applicationId:guid}")]
    [Authorize(Policy = Config.AdminPolicy)]
    public async Task<ActionResult> DeleteApplication(Guid applicationId, CancellationToken ct)
    {
        var res = await applicationRepository.DeleteApplicationAsync(applicationId, ct);
        if (!res)
            return NotFound();
        return Ok();
    }
}