using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface ISigningKeyService
{
    public Task<SigningKey> GetActiveSigningKeyAsync(CancellationToken ct = default);
    public Task<IEnumerable<SigningKey>> GetAllKeysAsync(CancellationToken ct = default);
    public Task RotateSigningKeyAsync(CancellationToken ct = default);
    public SigningKey GetActiveSigningKey();
}