using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IUserRepository
{
    public Task<User?> GetUserAsync(Guid userId, CancellationToken ct = default);
    public Task<Guid> CreateUserAsync(Guid applicationId, CancellationToken ct = default);
    public Task<bool> DeleteUserAsync(Guid userId, CancellationToken ct = default);
}