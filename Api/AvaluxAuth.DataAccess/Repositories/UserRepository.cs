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
}