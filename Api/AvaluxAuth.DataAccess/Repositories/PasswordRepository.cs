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
            .FirstOrDefaultAsync(ct);
        return entity == null ? null : FromEntity(entity);
    }

    public async Task<IReadOnlyList<PasswordUser>> GetByEmailAsync(string email,
        CancellationToken ct = default)
    {
        // The value is user input: a raw ILIKE pattern would let '%' and '_' act
        // as wildcards, so they are escaped and the escape character is passed
        // explicitly.
        var pattern = EscapeLikePattern(email.Trim());
        var entities = await dbContext.Passwords
            .Where(p => p.DeletedAt == null && p.Email != null)
            .Where(p => EF.Functions.ILike(p.Email!, pattern, "\\"))
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Take(MaxAccountsPerEmail)
            .ToListAsync(ct);
        return entities.Select(FromEntity).ToArray();
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
                // R-02: a confirmed flag belongs to one address. Changing it
                // drops the flag, so "confirmed" can never describe a different
                // mailbox. The new row value is compared inside the UPDATE, so
                // the expression still sees the old address.
                x.SetProperty(p => p.EmailConfirmedAt,
                    p => p.Email == info.Email ? p.EmailConfirmedAt : null);
                x.SetProperty(p => p.Email, info.Email);
                x.SetProperty(p => p.AvatarId, info.AvatarId);
            }, ct);
        await dbContext.SaveChangesAsync(ct);
        return count > 0;
    }

    public async Task<bool> MarkEmailConfirmedAsync(Guid id, DateTime confirmedAt, CancellationToken ct = default)
    {
        var count = await dbContext.Passwords
            .Where(p => p.Id == id && p.DeletedAt == null && p.Email != null)
            .ExecuteUpdateAsync(x =>
            {
                x.SetProperty(p => p.EmailConfirmedAt, confirmedAt);
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

    public async Task<IReadOnlyList<Guid>> GetAvatarIdsAsync(CancellationToken ct = default)
    {
        // Soft-deleted rows are included on purpose: restoring an account must
        // not point at an avatar that has already been cleaned up.
        return await dbContext.Passwords
            .Where(p => p.AvatarId != null)
            .Select(p => p.AvatarId!.Value)
            .ToListAsync(ct);
    }

    // Bounds the fan-out of a recovery request to one address; an e-mail is not
    // unique in this table, so several accounts may share it.
    private const int MaxAccountsPerEmail = 5;

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
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
            EmailConfirmedAt = entity.EmailConfirmedAt,
        };
    }
}