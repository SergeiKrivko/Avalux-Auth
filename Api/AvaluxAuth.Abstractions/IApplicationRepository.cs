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

    /// <summary>
    /// Sets the application icon URL, or clears it when <paramref name="iconUrl"/>
    /// is <c>null</c>.
    /// </summary>
    public Task<bool> UpdateApplicationIconAsync(Guid applicationId, string? iconUrl,
        CancellationToken ct = default);

    public Task<bool> UpdateClientSecretAsync(Guid applicationId, string clientSecret, CancellationToken ct = default);

    public Task<bool> DeleteApplicationAsync(Guid applicationId, CancellationToken ct = default);
}