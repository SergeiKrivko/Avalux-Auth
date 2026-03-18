using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IOauthService
{
    public Task<string> CreateLinkCode(Guid userId, CancellationToken ct = default);

    public Task<string> GetAuthUrlAsync(string clientId, string providerKey, string redirectUri, string? state, string? linkCode,
        CancellationToken ct = default);

    public Task<ProcessedAuthorizationState> ProcessCodeAsync(Dictionary<string, string> query, string state,
        CancellationToken ct = default);

    public Task<bool> CheckClientSecretAsync(string clientId, string clientSecret,
        CancellationToken ct = default);
}