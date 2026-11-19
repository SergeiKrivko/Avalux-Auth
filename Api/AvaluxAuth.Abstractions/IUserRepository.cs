using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IUserRepository
{
    public Task<User?> GetUserAsync(Guid userId, CancellationToken ct = default);
    public Task<UserWithSubscriptions?> GetUserWithSubscriptionsAsync(Guid userId, CancellationToken ct = default);
    public Task<UserWithAccounts?> GetUserWithAccountsAsync(Guid userId, CancellationToken ct = default);
    public Task<Guid> CreateUserAsync(Guid applicationId, CancellationToken ct = default);
    public Task<bool> DeleteUserAsync(Guid userId, CancellationToken ct = default);
    public Task<int> CountUsersAsync(Guid applicationId, CancellationToken ct = default);
    public Task<IEnumerable<UserWithAccounts>> GetUsersAsync(Guid applicationId, CancellationToken ct = default);

    public Task<IEnumerable<UserWithAccounts>> GetUsersAsync(Guid applicationId, int page, int limit,
        CancellationToken ct = default);

    public Task<IEnumerable<UserWithAccounts>> SearchUsersAsync(Guid applicationId, string? username, string? login,
        string? email, Guid? providerId, int page, int? limit,
        CancellationToken ct = default);

    /// <summary>
    /// Session generation of a live user, or <c>null</c> when the user does not
    /// exist or was deleted (M-03).
    /// </summary>
    public Task<int?> GetSessionVersionAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Revokes every session of the user: bumps the session version (so already
    /// issued access tokens stop validating) and deletes all refresh tokens.
    /// Both changes are applied atomically. Returns <c>false</c> when the user
    /// does not exist or was deleted.
    /// </summary>
    public Task<bool> RevokeSessionsAsync(Guid userId, CancellationToken ct = default);
}