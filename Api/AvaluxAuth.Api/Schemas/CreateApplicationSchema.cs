namespace AvaluxAuth.Api.Schemas;

public class CreateApplicationSchema
{
    public required string Name { get; init; }

    /// <summary>Defaults to a confidential client; turn off for SPAs and desktop apps that use PKCE.</summary>
    public bool RequireClientSecret { get; init; } = true;
}