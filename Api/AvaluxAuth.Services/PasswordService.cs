using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AvaluxAuth.Services;

public class PasswordService(
    IAccountRepository accountRepository,
    IUserRepository userRepository,
    IProviderRepository providerRepository,
    IPasswordRepository passwordRepository,
    IConfiguration configuration,
    IImageService imageService,
    ILogger<PasswordService> logger) : IPasswordService
{
    // Verified against for unknown logins, so a failed sign-in always pays the
    // BCrypt cost and its duration does not reveal whether the account exists
    // (H-06 in SECURITY-AUDIT.md).
    private static readonly string DummyPasswordHash =
        BCrypt.Net.BCrypt.HashPassword("avaluxauth-timing-dummy-password");

    public async Task<bool> CheckUserExistsAsync(string login, CancellationToken ct = default)
    {
        var user = await passwordRepository.GetByLoginAsync(login, ct);
        return user != null;
    }

    public async Task<PasswordUser> CreateUserAsync(string login, string password, PasswordUserInfo info,
        CancellationToken ct = default)
    {
        // Defense in depth: the controller validates first, but no caller may
        // create an account with a password that violates the policy.
        var policyError = PasswordPolicy.Validate(password);
        if (policyError != null)
            throw new ArgumentException(policyError, nameof(password));

        // The address is the recovery channel, so it must be deliverable and fit
        // the columns (R-01 in SECURITY-AUDIT.md). The controller checks the same
        // rule first; no caller may create an account that recovery cannot reach
        // or that fails on insert with a 500.
        var emailError = EmailPolicy.Validate(info.Email);
        if (emailError != null)
            throw new ArgumentException(emailError, nameof(info));

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        var id = await passwordRepository.CreateAsync(login, passwordHash, info, ct);
        return await passwordRepository.GetByIdAsync(id, ct) ?? throw new Exception("Created account not found. What?");
    }

    public async Task<PasswordUser?> VerifyPasswordAsync(string login, string password, CancellationToken ct = default)
    {
        var user = await passwordRepository.GetByLoginAsync(login, ct);
        var hash = user?.PasswordHash ?? DummyPasswordHash;
        if (!VerifyHash(password, hash))
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
                ? $"{configuration["Api:ApiUrl"]}/api/v1/avatar/{password.Info.AvatarId}"
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

        // A supplied userId means the caller is linking this new password account to
        // an existing user (link_code flow), not creating a user (M-03).
        var linkedToExistingUser = userId.HasValue;
        userId ??= await userRepository.CreateUserAsync(applicationId, ct);
        await accountRepository.CreateAccountAsync(userId.Value, providerInfo.Id, userInfo, null, ct);

        // M-03: linking a credential to an existing account revokes its sessions.
        // The caller re-issues tokens for the new account right after this call, so
        // the flow that performed the link keeps working with a fresh token.
        if (linkedToExistingUser)
            await RevokeSessionsAsync(userId.Value, "account-linked", ct);

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

    public async Task<bool> UpdateInfoAsync(Guid id, Guid userId, Guid applicationId, PasswordUserInfo info,
        CancellationToken ct = default)
    {
        var emailError = EmailPolicy.Validate(info.Email);
        if (emailError != null)
            throw new ArgumentException(emailError, nameof(info));

        var result = await passwordRepository.UpdateInfoAsync(id, info, ct);
        if (!result)
            return result;
        var provider = await providerRepository.GetProviderByProviderIdAsync(applicationId, 0, ct);
        if (provider == null)
            return result;
        var account = (await accountRepository.GetAccountsOfUserAsync(userId, ct))
            .FirstOrDefault(e => e.ProviderId == provider.Id);
        if (account != null)
            await accountRepository.UpdateAccountInfoAsync(account.Id, new UserInfo
            {
                Id = id.ToString(),
                Name = info.Name,
                Email = info.Email,
                Login = account.Info.Login,
                AvatarUrl = info.AvatarId == null
                    ? account.Info.AvatarUrl
                    : $"{configuration["Api:ApiUrl"]}/api/v1/avatar/{info.AvatarId}"
            }, ct);
        return result;
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, PasswordUser user, string oldPassword, string newPassword,
        CancellationToken ct = default)
    {
        var policyError = PasswordPolicy.Validate(newPassword);
        if (policyError != null)
            throw new ArgumentException(policyError, nameof(newPassword));

        if (string.IsNullOrEmpty(oldPassword) || !VerifyHash(oldPassword, user.PasswordHash))
            throw new Exception("Wrong old password");
        var hash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        var changed = await passwordRepository.ChangePasswordAsync(user.Id, hash, ct);
        if (changed)
            await RevokeSessionsAsync(userId, "password-changed", ct);
        return changed;
    }

    /// <summary>
    /// M-03: a credential change invalidates every session of the user — refresh
    /// tokens are deleted and the session version is bumped, so access tokens
    /// issued before the change are rejected by the API.
    /// </summary>
    private async Task RevokeSessionsAsync(Guid userId, string reason, CancellationToken ct)
    {
        var revoked = await userRepository.RevokeSessionsAsync(userId, ct);
        if (revoked)
            logger.LogWarning("All sessions revoked: userId={UserId}, reason={Reason}", userId, reason);
        else
            logger.LogWarning("Session revocation skipped for a missing user: userId={UserId}, reason={Reason}",
                userId, reason);
    }

    private static bool VerifyHash(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // A malformed hash in the database must not turn a failed sign-in
            // into a 500 that is distinguishable from a wrong password.
            return false;
        }
    }
}