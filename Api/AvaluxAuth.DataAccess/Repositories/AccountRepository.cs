using AvaluxAuth.Abstractions;
using AvaluxAuth.DataAccess.Entities;
using AvaluxAuth.Models;
using Microsoft.EntityFrameworkCore;

namespace AvaluxAuth.DataAccess.Repositories;

public class AccountRepository(AvaluxAuthDbContext dbContext) : IAccountRepository
{
    public async Task<Account?> GetAccountByIdAsync(Guid id, CancellationToken ct = default)
    {
        var res = await dbContext.Accounts
            .Where(a => a.Id == id && a.DeletedAt == null)
            .FirstOrDefaultAsync(ct);
        return res is null ? null : FromEntity(res);
    }

    public async Task<Account?> GetAccountByProviderIdAsync(string id, CancellationToken ct = default)
    {
        var res = await dbContext.Accounts
            .Where(a => a.ProviderUserId == id && a.DeletedAt == null)
            .FirstOrDefaultAsync(ct);
        return res is null ? null : FromEntity(res);
    }

    public async Task<IEnumerable<Account>> GetAccountsOfUserAsync(Guid userId, CancellationToken ct = default)
    {
        var res = await dbContext.Accounts
            .Where(a => a.UserId == userId && a.DeletedAt == null)
            .ToListAsync(ct);
        return res.Select(FromEntity);
    }

    public async Task<Guid> CreateAccountAsync(Guid userId, Guid providerId, UserInfo account, AccountCredentials accountCredentials,
        CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var entity = new AccountEntity
        {
            Id = id,
            UserId = userId,
            ProviderId = providerId,
            ProviderUserId = account.Id,
            Name = account.Name,
            Email = account.Email,
            AvatarUrl = account.AvatarUrl,
            AccessToken = accountCredentials.AccessToken,
            RefreshToken = accountCredentials.RefreshToken,
            ExpiresAt = accountCredentials.ExpiresAt,
            CreatedAt = DateTime.UtcNow,
            DeletedAt = null,
        };
        await dbContext.Accounts.AddAsync(entity, ct);
        await dbContext.SaveChangesAsync(ct);
        return id;
    }

    public async Task<bool> UpdateAccountTokensAsync(Guid accountId, AccountCredentials accountCredentials,
        CancellationToken ct = default)
    {
        var count = await dbContext.Accounts
            .Where(a => a.Id == accountId && a.DeletedAt == null)
            .ExecuteUpdateAsync(a =>
            {
                a.SetProperty(x => x.AccessToken, accountCredentials.AccessToken);
                a.SetProperty(x => x.RefreshToken, accountCredentials.RefreshToken);
                a.SetProperty(x => x.ExpiresAt, accountCredentials.ExpiresAt);
            }, ct);
        await dbContext.SaveChangesAsync(ct);
        return count > 0;
    }

    public async Task<bool> UpdateAccountInfoAsync(Guid accountId, UserInfo info, CancellationToken ct = default)
    {
        var count = await dbContext.Accounts
            .Where(a => a.Id == accountId && a.DeletedAt == null)
            .ExecuteUpdateAsync(a =>
            {
                a.SetProperty(x => x.Name, info.Name);
                a.SetProperty(x => x.Email, info.Email);
                a.SetProperty(x => x.AvatarUrl, info.AvatarUrl);
            }, ct);
        await dbContext.SaveChangesAsync(ct);
        return count > 0;
    }

    public async Task<bool> DeleteAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        var count = await dbContext.Accounts
            .Where(a => a.Id == accountId && a.DeletedAt == null)
            .ExecuteUpdateAsync(a =>
            {
                a.SetProperty(x => x.DeletedAt, DateTime.UtcNow);
            }, ct);
        await dbContext.SaveChangesAsync(ct);
        return count > 0;
    }

    private static Account FromEntity(AccountEntity entity)
    {
        return new Account
        {
            Id = entity.Id,
            UserId = entity.UserId,
            ProviderId = entity.ProviderId,
            Info = new UserInfo
            {
                Id = entity.ProviderUserId,
                Name = entity.Name,
                Email = entity.Email,
                AvatarUrl = entity.AvatarUrl,
            },
            TokenPair = new AccountCredentials
            {
                AccessToken = entity.AccessToken,
                RefreshToken = entity.RefreshToken,
                ExpiresAt = entity.ExpiresAt,
            }
        };
    }
}