using System.Text.Json;
using Avalux.Auth.UserClient.Models;

namespace AvaluxAuth.TestCli;

public static class Utils
{
    public const string CallbackUrl = "http://localhost:14887";

    private static string ConfigFilePath { get; } = Path.Join(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SergeiKrivko", "AvaluxAuth",
        "config.json");

    public static async Task SaveCredentials(UserCredentials credentials)
    {
        var directoryName = Path.GetDirectoryName(ConfigFilePath);
        if (!string.IsNullOrEmpty(directoryName))
            Directory.CreateDirectory(directoryName);
        await File.WriteAllTextAsync(ConfigFilePath, JsonSerializer.Serialize(credentials));
    }

    public static async Task<UserCredentials?> LoadCredentials()
    {
        if (!File.Exists(ConfigFilePath))
            return null;
        var text = await File.ReadAllTextAsync(ConfigFilePath);
        return JsonSerializer.Deserialize<UserCredentials>(text);
    }
}