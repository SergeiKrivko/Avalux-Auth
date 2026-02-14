namespace AvaluxAuth.Abstractions;

public interface IUserService
{
    public Task<string?> GetAccessTokenAsync(Guid userId, string providerKey, CancellationToken ct);
    public Task<string?> GetAccessTokenAsync(Guid userId, Guid providerId, CancellationToken ct);
}