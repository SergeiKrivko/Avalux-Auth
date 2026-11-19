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

    /// <summary>
    /// Counts an avatar upload attempt for the state and returns whether it stays
    /// within <paramref name="limit"/>. M-01 in SECURITY-AUDIT.md: an anonymous
    /// upload endpoint is a free image host, so uploads are tied to a live
    /// authorization state and capped per state. Attempts are counted before the
    /// image is decoded, so malformed uploads do not bypass the quota.
    /// </summary>
    public Task<bool> TryRegisterUploadAsync(string state, int limit);
}