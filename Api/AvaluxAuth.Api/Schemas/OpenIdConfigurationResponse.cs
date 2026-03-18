using System.Text.Json.Serialization;

namespace AvaluxAuth.Api.Schemas;

public sealed record OpenIdConfigurationResponse
{
    [JsonPropertyName("issuer")] public string? Issuer { get; init; }
    [JsonPropertyName("authorization_endpoint")] public string? AuthorizationEndpoint { get; init; }
    [JsonPropertyName("token_endpoint")] public string? TokenEndpoint { get; init; }
    [JsonPropertyName("jwks_uri")] public string? JwksUri { get; init; }

    [JsonPropertyName("response_types_supported")]
    public string[] ResponseTypeSupported => ["code"];

    [JsonPropertyName("subject_type_supported")]
    public string[] SubjectTypeSupported => ["public"];

    [JsonPropertyName("id_token_signing_alg_values_supported")]
    public string[] IdTokenSigningAlgValuesSupported => ["RS256"];

    [JsonPropertyName("grant_types_supported")]
    public string[] GrantTypesSupported => ["authorization_code", "refresh_token"];

    [JsonPropertyName("code_challenge_methods_supported")]
    public string[] CodeChallengeMethodsSupported => ["S256"];
}