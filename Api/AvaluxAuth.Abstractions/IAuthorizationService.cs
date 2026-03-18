using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IAuthorizationService
{
    public Task<string> CreateAuthorizationCodeAsync(Guid userId, CancellationToken ct = default);
    public Task<UserCredentials> GetTokenAsync(string code, CancellationToken ct = default);
    public Task<UserCredentials> GetTokenAsync(Guid userId, CancellationToken ct = default);
    public Task<UserCredentials?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    public Task<bool> RevokeTokenAsync(string refreshToken, CancellationToken ct = default);
}