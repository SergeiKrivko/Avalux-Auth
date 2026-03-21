using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IPasswordService
{
    public Task<bool> CheckUserExistsAsync(string login, CancellationToken ct = default);

    public Task<PasswordUser> CreateUserAsync(string login, string password, PasswordUserInfo info,
        CancellationToken ct = default);

    public Task<PasswordUser?> VerifyPasswordAsync(string login, string password, CancellationToken ct = default);

    public Task<Guid> GetOrCreateAccountAsync(Guid applicationId, Guid? userId, PasswordUser password,
        CancellationToken ct = default);

    public Task<PasswordUser?> GetByUserId(Guid userId, CancellationToken ct = default);

    public Task<bool> UpdateInfoAsync(Guid id, Guid userId, Guid applicationId, PasswordUserInfo info,
        CancellationToken ct = default);

    public Task<bool> ChangePasswordAsync(PasswordUser user, string oldPassword, string newPassword,
        CancellationToken ct = default);
}