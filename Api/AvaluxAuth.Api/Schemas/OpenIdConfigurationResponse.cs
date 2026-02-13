using System.Text.Json.Serialization;

namespace AvaluxAuth.Api.Schemas;

public sealed record OpenIdConfigurationResponse
{
    [JsonPropertyName("issuer")] public string? Issuer { get; init; }
    [JsonPropertyName("authorization_endpoint")] public string? AuthorizationEndpoint { get; init; }
    [JsonPropertyName("token_endpoint")] public string? TokenEndpoint { get; init; }
    [JsonPropertyName("jwks_uri")] public string? JwksUri { get; init; }
}