using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IProviderRepository
{
    public Task<Provider?> GetProviderByIdAsync(Guid id, CancellationToken ct = default);
    public Task<Provider?> GetProviderByProviderIdAsync(Guid applicationId, int id, CancellationToken ct = default);
    public Task<IEnumerable<Provider>> GetAllProvidersAsync(Guid applicationId, CancellationToken ct = default);

    public Task<Guid> CreateProviderAsync(Guid applicationId, int providerId, ProviderParameters provider,
        CancellationToken ct = default);

    public Task<bool> UpdateProviderAsync(Guid id, ProviderParameters provider, CancellationToken ct = default);
    public Task<bool> DeleteProviderAsync(Guid id, CancellationToken ct = default);
}