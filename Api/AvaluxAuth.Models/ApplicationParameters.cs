namespace AvaluxAuth.Models;

public class ApplicationParameters
{
    public required string Name { get; init; }
    public string[] RedirectUrls { get; init; } = [];
}