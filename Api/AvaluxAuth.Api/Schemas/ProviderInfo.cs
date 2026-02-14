namespace AvaluxAuth.Api.Schemas;

public class ProviderInfo
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string Key { get; init; }
    public string? Url { get; init; }
}