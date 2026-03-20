using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IPasswordRepository
{
    public Task<PasswordUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
    public Task<PasswordUser?> GetByLoginAsync(string login, CancellationToken ct = default);

    public Task<Guid> CreateAsync(string login, string passwordHash, PasswordUserInfo info,
        CancellationToken ct = default);
    public Task<bool> UpdateInfoAsync(Guid id, PasswordUserInfo info, CancellationToken ct = default);
    public Task<bool> ChangePasswordAsync(Guid id, string newPasswordHash, CancellationToken ct = default);
    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}