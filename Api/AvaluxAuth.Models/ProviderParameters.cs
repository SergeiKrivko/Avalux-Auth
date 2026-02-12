namespace AvaluxAuth.Models;

public class ProviderParameters
{
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }

    public bool SaveTokens { get; init; }
}