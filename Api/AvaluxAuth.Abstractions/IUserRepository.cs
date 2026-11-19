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

    /// <summary>
    /// One page of live users of the application, ordered by <c>CreatedAt</c> then id.
    /// <paramref name="page"/> is clamped to <c>&gt;= 0</c> and <paramref name="limit"/>
    /// to <c>[1, Pagination.MaxLimit]</c>, so an out-of-range request cannot turn into a
    /// negative OFFSET or an unbounded scan (L-04).
    /// </summary>
    public Task<IEnumerable<UserWithAccounts>> GetUsersAsync(Guid applicationId, int page, int limit,
        CancellationToken ct = default);

    /// <summary>
    /// The page <paramref name="page"/> of live users that have an account matching the
    /// filters, ordered by the user's <c>CreatedAt</c> then id. A page is a page of
    /// users, not of accounts: every matching account of the returned users is
    /// included. A <c>null</c> <paramref name="limit"/> means
    /// <c>Pagination.MaxLimit</c>, not "everything" (L-04).
    /// </summary>
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