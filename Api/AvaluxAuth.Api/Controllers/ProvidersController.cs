using AvaluxAuth.Abstractions;
using AvaluxAuth.Api.Schemas;
using AvaluxAuth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("api/v1/admin/apps/{applicationId:guid}/providers")]
[Authorize(Policy = Config.AdminPolicy)]
[EnableCors(PolicyName = Config.AdminPolicy)]
public class ProvidersController(IProviderRepository providerRepository) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Guid>> CreateProvider(Guid applicationId, [FromBody] CreateProviderSchema schema,
        CancellationToken ct)
    {
        var res = await providerRepository.CreateProviderAsync(applicationId, schema.ProviderId, schema.Parameters, ct);
        return Ok(res);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Provider>>> GetProviders(Guid applicationId, CancellationToken ct)
    {
        var res = await providerRepository.GetAllProvidersAsync(applicationId, ct);
        return Ok(res);
    }

    [HttpGet("{providerId:guid}")]
    public async Task<ActionResult<Provider>> GetProvider(Guid applicationId, Guid providerId, CancellationToken ct)
    {
        var res = await providerRepository.GetProviderByIdAsync(providerId, ct);
        if (res == null)
            return NotFound();
        return Ok(res);
    }

    [HttpPut("{providerId:guid}")]
    public async Task<ActionResult> UpdateProvider(Guid applicationId, Guid providerId,
        [FromBody] ProviderParameters parameters,
        CancellationToken ct)
    {
        var res = await providerRepository.UpdateProviderAsync(providerId, parameters, ct);
        if (!res)
            return NotFound();
        return Ok();
    }

    [HttpDelete("{providerId:guid}")]
    public async Task<ActionResult> DeleteProvider(Guid applicationId, Guid providerId, CancellationToken ct)
    {
        var res = await providerRepository.DeleteProviderAsync(providerId, ct);
        if (!res)
            return NotFound();
        return Ok();
    }
}