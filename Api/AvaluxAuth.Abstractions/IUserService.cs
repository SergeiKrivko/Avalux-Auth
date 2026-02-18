using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IUserService
{
    public Task<AccountCredentials?> GetAccessTokenAsync(Guid userId, string providerKey, CancellationToken ct);
    public Task<AccountCredentials?> GetAccessTokenAsync(Guid userId, Guid providerId, CancellationToken ct);
}