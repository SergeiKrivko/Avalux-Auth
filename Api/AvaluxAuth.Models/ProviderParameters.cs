namespace AvaluxAuth.Models;

public class ProviderParameters
{
    public string? ClientName { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }

    public bool SaveTokens { get; init; }
    public string[] DefaultScope { get; init; } = [];
}