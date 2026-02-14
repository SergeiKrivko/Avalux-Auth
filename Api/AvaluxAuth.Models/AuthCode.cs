namespace AvaluxAuth.Models;

public class AuthCode
{
    public required string Code { get; init; }
    public required Dictionary<string, string> Query { get; init; }
    public required AuthorizationState State { get; init; }
}