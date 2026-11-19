using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IAccountRepository
{
    public Task<Account?> GetAccountByIdAsync(Guid id, CancellationToken ct = default);
    public Task<Account?> GetAccountByProviderIdAsync(Guid providerId, string id, CancellationToken ct = default);
    public Task<IEnumerable<Account>> GetAccountsOfUserAsync(Guid userId, CancellationToken ct = default);

    public Task<Account?> FindAccountAsync(Guid userId, Guid providerId,
        CancellationToken ct = default);

    public Task<Guid> CreateAccountAsync(Guid userId, Guid providerId, UserInfo account,
        AccountCredentials? accountCredentials,
        CancellationToken ct = default);

    public Task<bool> UpdateAccountTokensAsync(Guid accountId, AccountCredentials accountCredentials,
        CancellationToken ct = default);

    public Task<bool> UpdateAccountInfoAsync(Guid accountId, UserInfo info, CancellationToken ct = default);
    public Task<bool> DeleteAccountAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>
    /// Avatar URLs of all accounts that contain <paramref name="avatarUrlMarker"/>
    /// (e.g. <c>/api/v1/avatar/</c>). Provider avatars are external URLs and are
    /// not stored in the file repository, so they are not returned. Used by the
    /// avatar cleanup (M-01 in SECURITY-AUDIT.md).
    /// </summary>
    public Task<IReadOnlyList<string>> GetAvatarUrlsAsync(string avatarUrlMarker,
        CancellationToken ct = default);
}