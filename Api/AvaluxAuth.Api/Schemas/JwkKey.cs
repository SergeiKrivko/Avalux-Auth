namespace AvaluxAuth.Api.Schemas;

public class JwkKey
{
    public required string Kty { get; init; }
    public required string Use { get; init; }
    public required string Kid { get; init; }
    public required string Alg { get; init; }
    public required string N { get; init; }
    public required string E { get; init; }
}