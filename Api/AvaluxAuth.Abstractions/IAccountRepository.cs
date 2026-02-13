using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IAccountRepository
{
    public Task<Account?> GetAccountByIdAsync(Guid id, CancellationToken ct = default);
    public Task<Account?> GetAccountByProviderIdAsync(Guid applicationId, string id, CancellationToken ct = default);
    public Task<IEnumerable<Account>> GetAccountsOfUserAsync(Guid userId, CancellationToken ct = default);

    public Task<Guid> CreateAccountAsync(Guid userId, Guid providerId, UserInfo account, AccountCredentials accountCredentials,
        CancellationToken ct = default);

    public Task<bool> UpdateAccountTokensAsync(Guid accountId, AccountCredentials accountCredentials, CancellationToken ct = default);
    public Task<bool> UpdateAccountInfoAsync(Guid accountId, UserInfo info, CancellationToken ct = default);
    public Task<bool> DeleteAccountAsync(Guid accountId, CancellationToken ct = default);
}