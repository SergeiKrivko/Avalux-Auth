using AvaluxAuth.Abstractions;
using AvaluxAuth.Api.Schemas;
using AvaluxAuth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("api/v1/admin/apps/{applicationId:guid}/tokens")]
[Authorize(Policy = Config.AdminPolicy)]
[EnableCors(PolicyName = Config.AdminPolicy)]
public class TokenController(ITokenRepository tokenRepository, ITokenService tokenService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Token>>> GetTokens(Guid applicationId, CancellationToken ct)
    {
        var tokens = await tokenRepository.GetTokensAsync(applicationId, ct);
        return Ok(tokens);
    }

    [HttpGet("{tokenId:guid}")]
    public async Task<ActionResult<Token>> GetTokenById(Guid applicationId, Guid tokenId, CancellationToken ct)
    {
        var token = await tokenRepository.GetTokenByIdAsync(tokenId, ct);
        return Ok(token);
    }

    [HttpPost]
    public async Task<ActionResult<string>> CreateToken(Guid applicationId, [FromBody] CreateTokenSchema schema,
        CancellationToken ct)
    {
        var token = await tokenService.CreateTokenAsync(applicationId, schema.Name, schema.Permissions,
            schema.ExpiresAt, ct);
        return Ok(token);
    }
}