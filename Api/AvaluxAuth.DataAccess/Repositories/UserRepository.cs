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
        var count = await dbContext.Users
            .Where(x => x.Id == userId && x.DeletedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(e => e.DeletedAt, DateTime.UtcNow), ct);
        return count > 0;
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
        var users = await dbContext.Users
            .Where(x => x.ApplicationId == applicationId && x.DeletedAt == null)
            .Skip(page * limit)
            .Take(limit)
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
        if (limit != null)
            query = query.Take(limit.Value).Skip(page * limit.Value);
        var result = await query
            .GroupBy(e => e.UserId)
            .ToListAsync(ct);
        return result.Select(r =>
        {
            var accounts = r.ToList();
            var user = accounts[0].User;
            return new UserWithAccounts
            {
                Id = user.Id,
                ApplicationId = user.ApplicationId,
                CreatedAt = user.CreatedAt,
                DeletedAt = user.DeletedAt,
                Accounts = accounts.Select(e => new AccountInfo
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