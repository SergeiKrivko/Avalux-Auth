using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IStateRepository
{
    public Task SaveStateAsync(AuthorizationState state);
    public Task<AuthorizationState?> GetStateAsync(string state);
    public Task DeleteStateAsync(string state);
    public Task<AuthorizationState?> TakeStateAsync(string state);

    /// <summary>
    /// Registers a failed sign-in for the state and returns the total number of
    /// failures. H-06 in SECURITY-AUDIT.md: a state must not allow unlimited
    /// password guesses.
    /// </summary>
    public Task<long> RegisterFailureAsync(string state);
}