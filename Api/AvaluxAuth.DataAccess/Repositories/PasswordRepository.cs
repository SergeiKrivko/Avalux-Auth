using AvaluxAuth.Abstractions;
using AvaluxAuth.DataAccess.Entities;
using AvaluxAuth.Models;
using Microsoft.EntityFrameworkCore;

namespace AvaluxAuth.DataAccess.Repositories;

public class PasswordRepository(AvaluxAuthDbContext dbContext) : IPasswordRepository
{
    public async Task<PasswordUser?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await dbContext.Passwords
            .Where(p => p.Id == id && p.DeletedAt == null)
            .SingleOrDefaultAsync(ct);
        return entity == null ? null : FromEntity(entity);
    }

    public async Task<PasswordUser?> GetByLoginAsync(string login, CancellationToken ct = default)
    {
        var entity = await dbContext.Passwords
            .Where(p => p.Login == login && p.DeletedAt == null)
            .SingleOrDefaultAsync(ct);
        return entity == null ? null : FromEntity(entity);
    }

    public async Task<Guid> CreateAsync(string login, string passwordHash, PasswordUserInfo info,
        CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var entity = new PasswordEntity
        {
            Id = id,
            Login = login,
            PasswordHash = passwordHash,
            Name = info.Name,
            Email = info.Email,
            AvatarId = info.AvatarId,
            CreatedAt = DateTime.UtcNow,
            DeletedAt = null,
        };
        await dbContext.Passwords.AddAsync(entity, ct);
        await dbContext.SaveChangesAsync(ct);
        return id;
    }

    public async Task<bool> UpdateInfoAsync(Guid id, PasswordUserInfo info, CancellationToken ct = default)
    {
        var count = await dbContext.Passwords
            .Where(p => p.Id == id && p.DeletedAt == null)
            .ExecuteUpdateAsync(x =>
            {
                x.SetProperty(p => p.Name, info.Name);
                x.SetProperty(p => p.Email, info.Email);
                x.SetProperty(p => p.AvatarId, info.AvatarId);
            }, ct);
        await dbContext.SaveChangesAsync(ct);
        return count > 0;
    }

    public async Task<bool> ChangePasswordAsync(Guid id, string newPasswordHash, CancellationToken ct = default)
    {
        var count = await dbContext.Passwords
            .Where(p => p.Id == id && p.DeletedAt == null)
            .ExecuteUpdateAsync(x =>
            {
                x.SetProperty(p => p.PasswordHash, newPasswordHash);
            }, ct);
        await dbContext.SaveChangesAsync(ct);
        return count > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var count = await dbContext.Passwords
            .Where(p => p.Id == id && p.DeletedAt == null)
            .ExecuteUpdateAsync(x =>
            {
                x.SetProperty(p => p.DeletedAt, DateTime.UtcNow);
            }, ct);
        await dbContext.SaveChangesAsync(ct);
        return count > 0;
    }

    private static PasswordUser FromEntity(PasswordEntity entity)
    {
        return new PasswordUser
        {
            Id = entity.Id,
            Login = entity.Login,
            PasswordHash = entity.PasswordHash,
            Info = new PasswordUserInfo
            {
                Name = entity.Name,
                Email = entity.Email,
                AvatarId = entity.AvatarId,
            },
            CreatedAt = entity.CreatedAt,
            DeletedAt = entity.DeletedAt,
        };
    }
}