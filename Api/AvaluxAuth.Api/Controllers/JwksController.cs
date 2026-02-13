using AvaluxAuth.Abstractions;
using AvaluxAuth.Api.Schemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("api/v1/jwks")]
public class JwksController(ISigningKeyService signingKeyService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<JwkKey>>> GetPublicKeys(CancellationToken ct)
    {
        var keys = await signingKeyService.GetAllKeysAsync(ct);
        var jwks = new List<JwkKey>();

        foreach (var key in keys)
        {
            var jsonWebKey = new JsonWebKey(key.PublicJwkJson);
            jwks.Add(new JwkKey
            {
                Kty = jsonWebKey.Kty,
                Use = jsonWebKey.Use,
                Kid = jsonWebKey.Kid,
                Alg = jsonWebKey.Alg,
                N = jsonWebKey.N,
                E = jsonWebKey.E
            });
        }

        return Ok(jwks);
    }
}