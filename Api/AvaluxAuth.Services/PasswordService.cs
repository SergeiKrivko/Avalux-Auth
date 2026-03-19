using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;

namespace AvaluxAuth.Services;

public class PasswordService(
    IAccountRepository accountRepository,
    IUserRepository userRepository,
    IProviderRepository providerRepository,
    IImageService imageService) : IPasswordService
{
    public async Task<bool> AddPasswordAsync(Guid applicationId, Guid? userId, string login, string password,
        UserInfo userInfo, CancellationToken ct = default)
    {
        var providerSettings = await providerRepository.GetProviderByProviderIdAsync(applicationId, 0, ct);
        if (providerSettings == null)
            throw new Exception("Password authorization is not added for this application");

        var existingAccount = await accountRepository.GetAccountByProviderIdAsync(providerSettings.Id, login, ct);
        if (existingAccount != null)
            return false;

        userId ??= await userRepository.CreateUserAsync(applicationId, ct);

        if (string.IsNullOrEmpty(userInfo.Name))
            userInfo.Name = null;
        userInfo.Id = login;
        userInfo.Login ??= login;
        userInfo.AvatarUrl ??= imageService.CreateRandomAvatarUrl(userInfo.Name ?? login);
        var accountId =
            await accountRepository.CreateAccountAsync(userId.Value, providerSettings.Id, userInfo, null, ct);
        await accountRepository.ChangePasswordAsync(accountId, BCrypt.Net.BCrypt.HashPassword(password), ct);
        return true;
    }

    public async Task<Guid?> VerifyPasswordAsync(Guid applicationId, string login, string password,
        CancellationToken ct = default)
    {
        var providerSettings = await providerRepository.GetProviderByProviderIdAsync(applicationId, 0, ct);
        if (providerSettings == null)
            throw new Exception("Password authorization is not added for this application");

        var account = await accountRepository.GetAccountByProviderIdAsync(providerSettings.Id, login, ct);
        if (account == null)
            return null;

        return BCrypt.Net.BCrypt.Verify(password, account.PasswordHash) ? account.UserId : null;
    }
}