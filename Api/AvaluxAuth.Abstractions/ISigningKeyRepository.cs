using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface ISigningKeyRepository
{
    public Task<SigningKey?> GetActiveAsync(CancellationToken ct = default);
    public Task<IEnumerable<SigningKey>> GetAllAsync(CancellationToken ct = default);
    public Task AddAsync(SigningKey key, CancellationToken ct = default);
}