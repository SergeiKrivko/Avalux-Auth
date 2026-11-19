using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IPasswordRepository
{
    public Task<PasswordUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
    public Task<PasswordUser?> GetByLoginAsync(string login, CancellationToken ct = default);

    /// <summary>
    /// Password accounts whose e-mail matches <paramref name="email"/>
    /// case-insensitively (R-01 in SECURITY-AUDIT.md). The result is bounded: an
    /// address is not unique in this table, but a recovery request must not fan
    /// out into an unbounded number of letters.
    /// </summary>
    public Task<IReadOnlyList<PasswordUser>> GetByEmailAsync(string email, CancellationToken ct = default);

    public Task<Guid> CreateAsync(string login, string passwordHash, PasswordUserInfo info,
        CancellationToken ct = default);
    public Task<bool> UpdateInfoAsync(Guid id, PasswordUserInfo info, CancellationToken ct = default);
    public Task<bool> ChangePasswordAsync(Guid id, string newPasswordHash, CancellationToken ct = default);
    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Every avatar id referenced by a password account, including soft-deleted
    /// ones. Used by the avatar cleanup (M-01 in SECURITY-AUDIT.md) to tell
    /// claimed uploads from orphans.
    /// </summary>
    public Task<IReadOnlyList<Guid>> GetAvatarIdsAsync(CancellationToken ct = default);
}