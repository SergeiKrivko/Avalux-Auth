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

    public async Task<UserWithAccounts?> GetUserWithAccountsAsync(Guid userId, CancellationToken ct = default)
    {
        var res = await dbContext.Users
            .Where(x => x.Id == userId && x.DeletedAt == null)
            .Include(x => x.Accounts)
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
        string? email, Guid? providerId, int page, int? limit,
        CancellationToken ct = default)
    {
        var query = dbContext.Accounts
            .Include(e => e.User)
            .Where(e => e.User.ApplicationId == applicationId && e.DeletedAt == null);
        if (username != null)
            query = query.Where(e => e.Name != null && e.Name.StartsWith(username));
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
                        Email = e.Email,
                        AvatarUrl = e.AvatarUrl,
                    }
                }).ToArray(),
            };
        });
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
        return new UserWithAccounts
        {
            Id = entity.Id,
            ApplicationId = entity.ApplicationId,
            CreatedAt = entity.CreatedAt,
            DeletedAt = entity.DeletedAt,
            Accounts = entity.Accounts.Select(e => new AccountInfo
            {
                ProviderId = e.ProviderId,
                UserInfo = new UserInfo
                {
                    Id = e.ProviderUserId,
                    Name = e.Name,
                    Email = e.Email,
                    AvatarUrl = e.AvatarUrl,
                }
            }).ToArray(),
        };
    }
}