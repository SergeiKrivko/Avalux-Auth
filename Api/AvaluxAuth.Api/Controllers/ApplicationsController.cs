using AvaluxAuth.Abstractions;
using AvaluxAuth.Api.Schemas;
using AvaluxAuth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SixLabors.ImageSharp;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("api/v1/admin/apps")]
[EnableCors(PolicyName = Config.AdminPolicy)]
public class ApplicationsController(
    IApplicationRepository applicationRepository,
    IApplicationService applicationService,
    IUserRepository userRepository,
    ILogger<ApplicationsController> logger) : ControllerBase
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
            RequireClientSecret = schema.RequireClientSecret,
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
        // The service also drops the icon object, so builds do not stack up in S3.
        var res = await applicationService.DeleteApplicationAsync(applicationId, ct);
        if (!res)
            return NotFound();
        return Ok();
    }

    /// <summary>
    /// Uploads (or replaces) the application icon. The image is normalised to a
    /// 256px PNG and stored in S3 under the application id, so a replacement
    /// never leaves an unreferenced object behind.
    /// </summary>
    [HttpPost("{applicationId:guid}/icon")]
    [RequestSizeLimit(Config.MaxIconUploadBytes)]
    [EnableRateLimiting(Config.IconUploadRateLimitPolicy)]
    [Authorize(Policy = Config.AdminPolicy)]
    public async Task<ActionResult<string>> UploadIcon(Guid applicationId, IFormFile file,
        CancellationToken ct = default)
    {
        if (file.Length == 0)
            return BadRequest("Empty file");

        try
        {
            await using var stream = file.OpenReadStream();
            var iconUrl = await applicationService.SetIconAsync(applicationId, stream, ct);
            if (iconUrl is null)
                return NotFound();
            return Ok(iconUrl);
        }
        catch (Exception e) when (e is UnknownImageFormatException or InvalidImageContentException)
        {
            // A non-image or a truncated image is a client error, not a 500.
            logger.LogWarning("Rejected application icon upload: applicationId={ApplicationId}, type={Type}",
                applicationId, e.GetType().Name);
            return BadRequest("Unsupported image");
        }
    }

    [HttpDelete("{applicationId:guid}/icon")]
    [Authorize(Policy = Config.AdminPolicy)]
    public async Task<ActionResult> DeleteIcon(Guid applicationId, CancellationToken ct)
    {
        var res = await applicationService.DeleteIconAsync(applicationId, ct);
        if (!res)
            return NotFound();
        return Ok();
    }

    [HttpDelete("{applicationId:guid}/clientSecret")]
    [Authorize(Policy = Config.AdminPolicy)]
    public async Task<ActionResult<string>> RecreateClientSecret(Guid applicationId, CancellationToken ct)
    {
        var res = await applicationService.RecreateClientSecretAsync(applicationId, ct);
        return Ok(res);
    }
}