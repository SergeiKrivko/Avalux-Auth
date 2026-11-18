namespace AvaluxAuth.Models;

public class AuthorizationState
{
    public required string State { get; init; }
    public string? UserState { get; init; }
    public string? UserNonce { get; init; }
    public required Guid ApplicationId { get; init; }
    public string? ClientId { get; init; }
    public required Guid ProviderId { get; init; }
    public Guid? LinkUserId { get; init; }
    public required string RedirectUrl { get; init; }
    public string? CodeChallenge { get; init; }
    public string? CodeChallengeMethod { get; init; }
}

public record ProcessedAuthorizationState(Guid UserId, AuthorizationState State);