using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IApplicationRepository
{
    public Task<Application?> GetApplicationByIdAsync(Guid applicationId, CancellationToken ct = default);
    public Task<Application?> GetApplicationByClientIdAsync(string clientId, CancellationToken ct = default);
    public Task<IEnumerable<Application>> GetAllApplicationsAsync(CancellationToken ct = default);

    public Task<Guid> CreateApplicationAsync(ApplicationParameters parameters, string clientId,
        string clientSecret, CancellationToken ct = default);

    public Task<bool> UpdateApplicationAsync(Guid applicationId, ApplicationParameters parameters,
        CancellationToken ct = default);

    public Task<bool> DeleteApplicationAsync(Guid applicationId, CancellationToken ct = default);
}