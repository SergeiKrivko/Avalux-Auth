using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using Microsoft.Extensions.Configuration;

namespace AvaluxAuth.Services;

public class PasswordService(
    IAccountRepository accountRepository,
    IUserRepository userRepository,
    IProviderRepository providerRepository,
    IPasswordRepository passwordRepository,
    IConfiguration configuration,
    IImageService imageService) : IPasswordService
{
    public async Task<bool> CheckUserExistsAsync(string login, CancellationToken ct = default)
    {
        var user = await passwordRepository.GetByLoginAsync(login, ct);
        return user != null;
    }

    public async Task<PasswordUser> CreateUserAsync(string login, string password, PasswordUserInfo info,
        CancellationToken ct = default)
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        var id = await passwordRepository.CreateAsync(login, passwordHash, info, ct);
        return await passwordRepository.GetByIdAsync(id, ct) ?? throw new Exception("Created account not found. What?");
    }

    public async Task<PasswordUser?> VerifyPasswordAsync(string login, string password, CancellationToken ct = default)
    {
        var user = await passwordRepository.GetByLoginAsync(login, ct);
        if (user == null)
            return null;
        if (!BCrypt.Net.BCrypt.Verify(password, user?.PasswordHash) || user == null)
            return null;
        return user;
    }

    public async Task<Guid> GetOrCreateAccountAsync(Guid applicationId, Guid? userId, PasswordUser password,
        CancellationToken ct = default)
    {
        var providerInfo = await providerRepository.GetProviderByProviderIdAsync(applicationId, 0, ct) ??
                           throw new Exception("Password authorization is not added for this client");

        var account = await accountRepository.GetAccountByProviderIdAsync(providerInfo.Id, password.Id.ToString(), ct);
        var userInfo = new UserInfo
        {
            Id = password.Id.ToString(),
            Name = password.Info.Name,
            Email = password.Info.Email,
            AvatarUrl = password.Info.AvatarId.HasValue
                ? $"{configuration["Api.ApiUrl"]}/api/v1/avatar/{password.Info.AvatarId}"
                : account?.Info.AvatarUrl ??
                  imageService.CreateRandomAvatarUrl(password.Info.Name ?? password.Login),
        };
        if (account != null)
        {
            if (userId.HasValue && userId != account.UserId)
                throw new Exception("Account is already linked to another user");
            await accountRepository.UpdateAccountInfoAsync(account.Id, userInfo, ct);
            return account.UserId;
        }

        userId ??= await userRepository.CreateUserAsync(applicationId, ct);
        var accountId =
            await accountRepository.CreateAccountAsync(userId.Value, providerInfo.Id, userInfo, null, ct);
        return userId.Value;
    }

    public async Task<PasswordUser?> GetByUserId(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepository.GetUserWithAccountsAsync(userId, ct);
        if (user == null)
            return null;
        var provider = await providerRepository.GetProviderByProviderIdAsync(user.ApplicationId, 0, ct);
        if (provider == null)
            return null;
        var account = user.Accounts.FirstOrDefault(e => e.ProviderId == provider.Id);
        if (account == null)
            return null;
        var password = await passwordRepository.GetByIdAsync(Guid.Parse(account.UserInfo.Id), ct);
        return password;
    }

    public async Task<bool> UpdateInfoAsync(Guid id, PasswordUserInfo info, CancellationToken ct = default)
    {
        return await passwordRepository.UpdateInfoAsync(id, info, ct);
    }
}