using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IApplicationService
{
    public Task<Guid> CreateApplicationAsync(ApplicationParameters parameters, CancellationToken ct = default);
    public Task<string> RecreateClientSecretAsync(Guid applicationId, CancellationToken ct = default);
}