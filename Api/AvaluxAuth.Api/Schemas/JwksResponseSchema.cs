using System.Text.Json.Serialization;

namespace AvaluxAuth.Api.Schemas;

public class JwksResponseSchema
{
    [JsonPropertyName("keys")] public required JwkKey[] Keys { get; init; }
}