using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;

namespace AvaluxAuth.Services;

public class InMemoryStateRepository : IStateRepository
{
    private readonly Dictionary<string, AuthorizationState> _states = [];

    public Task SaveStateAsync(AuthorizationState state)
    {
        _states[state.State] = state;
        return Task.CompletedTask;
    }

    public Task<AuthorizationState> GetStateAsync(string state)
    {
        return Task.FromResult(_states[state]);
    }

    public Task DeleteStateAsync(string state)
    {
        _states.Remove(state);
        return Task.CompletedTask;
    }

    public Task<AuthorizationState> TakeStateAsync(string state)
    {
        var result = _states[state];
        _states.Remove(state);
        return Task.FromResult(result);
    }
}