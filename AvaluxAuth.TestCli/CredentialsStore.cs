using System.Text.Json;
using Avalux.Auth.UserClient;
using Avalux.Auth.UserClient.Models;

namespace AvaluxAuth.TestCli;

public class CredentialsStore : ICredentialsStore
{
    private static string ConfigFilePath { get; } = Path.Join(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SergeiKrivko", "AvaluxAuth",
        "config.json");

    public async Task SaveCredentials(UserCredentials? credentials, CancellationToken ct)
    {
        var directoryName = Path.GetDirectoryName(ConfigFilePath);
        if (!string.IsNullOrEmpty(directoryName))
            Directory.CreateDirectory(directoryName);
        await File.WriteAllTextAsync(ConfigFilePath, JsonSerializer.Serialize(credentials), ct);
    }

    public async Task<UserCredentials?> LoadCredentials(CancellationToken ct)
    {
        if (!File.Exists(ConfigFilePath))
            return null;
        var text = await File.ReadAllTextAsync(ConfigFilePath, ct);
        return JsonSerializer.Deserialize<UserCredentials>(text);
    }
}