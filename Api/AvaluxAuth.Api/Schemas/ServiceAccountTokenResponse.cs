namespace AvaluxAuth.Api.Schemas;

public class ServiceAccountTokenResponse
{
    public required string Token { get; init; }
    public Guid Id { get; init; }
}