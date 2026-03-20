namespace AvaluxAuth.Models;

public class Account
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required Guid ProviderId { get; init; }
    public required UserInfo Info { get; init; }
    public required AccountCredentials TokenPair { get; init; }
}