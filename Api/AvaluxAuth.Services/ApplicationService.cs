using System.Security.Cryptography;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using AvaluxAuth.Utils;

namespace AvaluxAuth.Services;

public class ApplicationService(IApplicationRepository applicationRepository) : IApplicationService
{
    public async Task<Guid> CreateApplicationAsync(ApplicationParameters parameters, CancellationToken ct = default)
    {
        var rn = RandomNumberGenerator.Create();
        var clientId = rn.RandomString();
        var clientSecret = rn.RandomString();
        return await applicationRepository.CreateApplicationAsync(parameters, clientId, clientSecret, ct);
    }

    public async Task<string> RecreateClientSecretAsync(Guid applicationId, CancellationToken ct = default)
    {
        var clientSecret = RandomNumberGenerator.GetRandomString();
        await applicationRepository.UpdateClientSecretAsync(applicationId, clientSecret, ct);
        return clientSecret;
    }
}