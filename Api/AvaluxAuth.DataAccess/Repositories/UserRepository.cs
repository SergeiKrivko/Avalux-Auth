using AvaluxAuth.Abstractions;
using AvaluxAuth.DataAccess.Entities;
using AvaluxAuth.Models;
using Microsoft.EntityFrameworkCore;

namespace AvaluxAuth.DataAccess.Repositories;

public class UserRepository(AvaluxAuthDbContext dbContext) : IUserRepository
{
    public async Task<User?> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        var res = await dbContext.Users
            .Where(x => x.Id == userId && x.DeletedAt == null)
            .FirstOrDefaultAsync(ct);
        return res is null ? null : FromEntity(res);
    }

    public async Task<UserWithSubscriptions?> GetUserWithSubscriptionsAsync(Guid userId, CancellationToken ct = default)
    {
        var res = await dbContext.Users
            .Where(x => x.Id == userId && x.DeletedAt == null)
            .Include(x => x.Subscriptions)
            .ThenInclude(e => e.Plan)
            .FirstOrDefaultAsync(ct);
        return res is null ? null : FromEntityWithSubscriptions(res);
    }

    public async Task<UserWithAccounts?> GetUserWithAccountsAsync(Guid userId, CancellationToken ct = default)
    {
        var res = await dbContext.Users
            .Where(x => x.Id == userId && x.DeletedAt == null)
            .Include(x => x.Accounts)
            .Include(e => e.Subscriptions)
            .FirstOrDefaultAsync(ct);
        return res is null ? null : FromEntityWithAccounts(res);
    }

    public async Task<Guid> CreateUserAsync(Guid applicationId, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var entity = new UserEntity
        {
            Id = id,
            ApplicationId = applicationId,
            CreatedAt = DateTime.UtcNow,
            DeletedAt = null,
        };
        await dbContext.Users.AddAsync(entity, ct);
        return id;
    }

    public async Task<bool> DeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
        // Accounts and refresh tokens must go with the user. A surviving account still
        // matches its provider identity at the next sign-in, so the callback would mint
        // an authorization code for a user that no longer exists and /token would fail
        // with "User not found" on every retry (an orphaned identity the user cannot
        // recover from). Both statements run in one transaction with the user row.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        var deletedAt = DateTime.UtcNow;

        var count = await dbContext.Users
            .Where(x => x.Id == userId && x.DeletedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(e => e.DeletedAt, deletedAt), ct);
        if (count == 0)
        {
            await transaction.RollbackAsync(ct);
            return false;
        }

        await dbContext.Accounts
            .Where(x => x.UserId == userId && x.DeletedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(e => e.DeletedAt, deletedAt), ct);
        await dbContext.RefreshTokens
            .Where(e => e.UserId == userId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
        return true;
    }

    public async Task<IEnumerable<UserWithAccounts>> GetUsersAsync(Guid applicationId, CancellationToken ct = default)
    {
        var users = await dbContext.Users
            .Where(x => x.ApplicationId == applicationId && x.DeletedAt == null)
            .Include(e => e.Accounts)
            .Include(e => e.Subscriptions)
            .ToListAsync(ct);
        return users.Select(FromEntityWithAccounts);
    }

    public async Task<IEnumerable<UserWithAccounts>> GetUsersAsync(Guid applicationId, int page, int limit,
        CancellationToken ct = default)
    {
        // A page without a stable order is not a page: the same request could return
        // different users on every call (L-04). CreatedAt+Id is deterministic and
        // keeps the insertion order the admin UI used to show.
        var pageSize = Pagination.NormalizeLimit(limit);
        var users = await dbContext.Users
            .Where(x => x.ApplicationId == applicationId && x.DeletedAt == null)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip(Pagination.Skip(page, pageSize))
            .Take(pageSize)
            .Include(e => e.Accounts)
            .Include(e => e.Subscriptions)
            .ToListAsync(ct);
        return users.Select(FromEntityWithAccounts);
    }

    public async Task<int> CountUsersAsync(Guid applicationId, CancellationToken ct = default)
    {
        return await dbContext.Users
            .Where(x => x.ApplicationId == applicationId && x.DeletedAt == null)
            .CountAsync(ct);
    }

    public async Task<IEnumerable<UserWithAccounts>> SearchUsersAsync(Guid applicationId, string? username,
        string? login, string? email, Guid? providerId, int page, int? limit,
        CancellationToken ct = default)
    {
        var query = dbContext.Accounts
            .Include(e => e.User)
            .Where(e => e.User.ApplicationId == applicationId && e.DeletedAt == null);
        if (username != null)
            query = query.Where(e => e.Name != null && e.Name.StartsWith(username));
        if (login != null)
            query = query.Where(e => e.Login != null && e.Login.StartsWith(login));
        if (email != null)
            query = query.Where(e => e.Email != null && e.Email.StartsWith(email));
        if (providerId != null)
            query = query.Where(e => e.ProviderId == providerId.Value);

        // L-04: paging the accounts and only then grouping them by user returned a
        // variable number of users per page and could push a user's accounts onto
        // different pages. Page the users instead: pick the matching user ids for
        // this page (stable order, bounded size), then load just their accounts.
        var pageSize = Pagination.NormalizeLimit(limit);
        var userIds = await query
            .Select(e => new { e.UserId, e.User.CreatedAt })
            .Distinct()
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.UserId)
            .Skip(Pagination.Skip(page, pageSize))
            .Take(pageSize)
            .Select(x => x.UserId)
            .ToListAsync(ct);
        if (userIds.Count == 0)
            return [];

        var accounts = await query
            .Where(e => userIds.Contains(e.UserId))
            .ToListAsync(ct);
        var position = userIds
            .Select((userId, index) => (userId, index))
            .ToDictionary(x => x.userId, x => x.index);
        return accounts
            .GroupBy(e => e.UserId)
            .OrderBy(g => position[g.Key])
            .Select(r =>
            {
                var userAccounts = r.ToList();
                var user = userAccounts[0].User;
                return new UserWithAccounts
                {
                    Id = user.Id,
                    ApplicationId = user.ApplicationId,
                    CreatedAt = user.CreatedAt,
                    DeletedAt = user.DeletedAt,
                    Accounts = userAccounts.Select(e => new AccountInfo
                    {
                        ProviderId = e.ProviderId,
                        UserInfo = new UserInfo
                        {
                            Id = e.ProviderUserId,
                            Name = e.Name,
                            Login = e.Login,
                            Email = e.Email,
                            AvatarUrl = e.AvatarUrl,
                        }
                    }).ToArray(),
                };
            });
    }

    public async Task<int?> GetSessionVersionAsync(Guid userId, CancellationToken ct = default)
    {
        // A deleted user is reported as having no session at all, so their access
        // tokens are rejected on the next request instead of living out the hour.
        return await dbContext.Users
            .Where(x => x.Id == userId && x.DeletedAt == null)
            .Select(x => (int?)x.SessionVersion)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> RevokeSessionsAsync(Guid userId, CancellationToken ct = default)
    {
        // Access tokens are stateless: the version bump is what invalidates them.
        // Refresh tokens are rows, so they are deleted. Both statements run in one
        // transaction: a crash between them must not leave a refresh token that can
        // mint a token with the new version (M-03).
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        var bumped = await dbContext.Users
            .Where(x => x.Id == userId && x.DeletedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(e => e.SessionVersion, e => e.SessionVersion + 1), ct);
        if (bumped == 0)
        {
            await transaction.RollbackAsync(ct);
            return false;
        }

        await dbContext.RefreshTokens
            .Where(e => e.UserId == userId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
        return true;
    }

    private static User FromEntity(UserEntity entity)
    {
        return new User
        {
            Id = entity.Id,
            ApplicationId = entity.ApplicationId,
            CreatedAt = entity.CreatedAt,
            DeletedAt = entity.DeletedAt,
        };
    }

    private static UserWithAccounts FromEntityWithAccounts(UserEntity entity)
    {
        var now = DateTime.UtcNow;
        return new UserWithAccounts
        {
            Id = entity.Id,
            ApplicationId = entity.ApplicationId,
            CreatedAt = entity.CreatedAt,
            DeletedAt = entity.DeletedAt,
            Accounts = entity.Accounts
                .Where(e => e.DeletedAt == null)
                .Select(e => new AccountInfo
                {
                    ProviderId = e.ProviderId,
                    UserInfo = new UserInfo
                    {
                        Id = e.ProviderUserId,
                        Name = e.Name,
                        Login = e.Login,
                        Email = e.Email,
                        AvatarUrl = e.AvatarUrl,
                    }
                }).ToArray(),
            Subscriptions = entity.Subscriptions
                .Where(e => IsActiveAt(e, now))
                .Select(e => new UserSubscription
                {
                    Id = e.Id,
                    PlanId = e.PlanId,
                    UserId = e.UserId,
                    CreatedAt = e.CreatedAt,
                    StartsAt = e.StartsAt ?? e.CreatedAt,
                    ExpiresAt = e.ExpiresAt,
                }).ToArray(),
        };
    }

    private static UserWithSubscriptions FromEntityWithSubscriptions(UserEntity entity)
    {
        var now = DateTime.UtcNow;
        return new UserWithSubscriptions
        {
            Id = entity.Id,
            ApplicationId = entity.ApplicationId,
            CreatedAt = entity.CreatedAt,
            DeletedAt = entity.DeletedAt,
            Subscriptions = entity.Subscriptions
                .Where(e => IsActiveAt(e, now))
                .Select(e => new SubscriptionInfo
                {
                    Plan = e.Plan.Key,
                    ExpiresAt = e.ExpiresAt,
                }).ToArray(),
            SessionVersion = entity.SessionVersion,
        };
    }

    /// <summary>
    /// A subscription grants its plan only inside [StartsAt, ExpiresAt). A future
    /// subscription must not appear in the access token's Subscriptions claim (M-02 in
    /// SECURITY-AUDIT.md) nor in the admin user view, otherwise the plan is handed out
    /// before it is paid for. Rows created before the StartsAt column existed have it
    /// null and start at CreatedAt, matching the value projected as StartsAt.
    /// </summary>
    private static bool IsActiveAt(UserSubscriptionEntity subscription, DateTime now)
    {
        var startsAt = subscription.StartsAt ?? subscription.CreatedAt;
        return startsAt <= now && subscription.ExpiresAt > now;
    }
}