using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IPasswordService
{
    public Task<bool> AddPasswordAsync(Guid applicationId, Guid? userId, string login, string password,
        UserInfo userInfo, CancellationToken ct = default);

    public Task<Guid?> VerifyPasswordAsync(Guid applicationId, string login, string password,
        CancellationToken ct = default);
}