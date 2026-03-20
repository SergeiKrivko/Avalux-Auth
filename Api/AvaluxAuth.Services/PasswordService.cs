using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;

namespace AvaluxAuth.Services;

public class PasswordService(
    IAccountRepository accountRepository,
    IUserRepository userRepository,
    IProviderRepository providerRepository,
    IPasswordRepository passwordRepository,
    IImageService imageService) : IPasswordService
{
    public async Task<bool> CheckUserExistsAsync(string login, CancellationToken ct = default)
    {
        var user = await passwordRepository.GetByLoginAsync(login, ct);
        Console.WriteLine($"User = {user}");
        return user == null;
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
            AvatarUrl = account?.Info.AvatarUrl ??
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
        return accountId;
    }
}