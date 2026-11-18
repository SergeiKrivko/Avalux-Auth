using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;

namespace AvaluxAuth.Services;

public class InMemoryStateRepository : IStateRepository
{
    private readonly Dictionary<string, AuthorizationState> _states = [];
    private readonly Dictionary<string, long> _failures = [];

    public Task SaveStateAsync(AuthorizationState state)
    {
        _states[state.State] = state;
        return Task.CompletedTask;
    }

    public Task<AuthorizationState?> GetStateAsync(string state)
    {
        return Task.FromResult(_states.GetValueOrDefault(state));
    }

    public Task DeleteStateAsync(string state)
    {
        _states.Remove(state);
        _failures.Remove(state);
        return Task.CompletedTask;
    }

    public Task<AuthorizationState?> TakeStateAsync(string state)
    {
        var result = _states.GetValueOrDefault(state);
        _states.Remove(state);
        _failures.Remove(state);
        return Task.FromResult(result);
    }

    public Task<long> RegisterFailureAsync(string state)
    {
        var failures = _failures.GetValueOrDefault(state) + 1;
        _failures[state] = failures;
        return Task.FromResult(failures);
    }
}
