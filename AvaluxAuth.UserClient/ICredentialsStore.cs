using Avalux.Auth.UserClient.Models;

namespace Avalux.Auth.UserClient;

public interface ICredentialsStore
{
    public Task SaveCredentials(UserCredentials? credentials, CancellationToken ct);
    public Task<UserCredentials?> LoadCredentials(CancellationToken ct);
}