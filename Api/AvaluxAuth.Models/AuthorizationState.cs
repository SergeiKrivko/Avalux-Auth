namespace AvaluxAuth.Models;

public class AuthorizationState
{
    public required string State { get; init; }
    public required Guid ApplicationId { get; init; }
    public required Guid ProviderId { get; init; }
    public required string RedirectUrl { get; init; }
    public Guid? UserId { get; init; }
}