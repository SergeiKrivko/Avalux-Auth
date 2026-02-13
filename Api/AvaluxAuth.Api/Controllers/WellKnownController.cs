using AvaluxAuth.Abstractions;
using AvaluxAuth.Api.Schemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("api/v1/.well-known")]
public class WellKnownController(ISigningKeyService signingKeyService, IConfiguration configuration) : ControllerBase
{
    [HttpGet("jwks.json")]
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

    [HttpGet("openid-configuration")]
    public ActionResult<OpenIdConfigurationResponse> GetOpenIdConfiguration()
    {
        var apiUrl = configuration["Api:ApiUrl"];
        return Ok(new OpenIdConfigurationResponse
        {
            Issuer = configuration["Security.Issuer"] ?? "",
            AuthorizationEndpoint = $"{apiUrl}/api/v1/auth/<provider>/authorize",
            TokenEndpoint = $"{apiUrl}/api/v1/auth/token",
            JwksUri = $"{apiUrl}/api/v1/.well-known/jwks.json",
        });
    }
}