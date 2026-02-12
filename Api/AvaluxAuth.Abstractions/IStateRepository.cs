using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IStateRepository
{
    public Task SaveStateAsync(AuthorizationState state);
    public Task<AuthorizationState> GetStateAsync(string state);
    public Task DeleteStateAsync(string state);
    public Task<AuthorizationState> TakeStateAsync(string state);
}