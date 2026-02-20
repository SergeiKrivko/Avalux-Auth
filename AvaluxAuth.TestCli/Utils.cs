using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using AvaluxAuth.Models;

namespace AvaluxAuth.TestCli;

public static class Utils
{
    public const string CallbackUrl = "http://localhost:14887";

    public static void OpenUrl(string url)
    {
        if (OperatingSystem.IsWindows())
            Process.Start(new ProcessStartInfo
                { FileName = "cmd", Arguments = $"/c start {url[0]}\"{url.AsSpan(1)}\"", CreateNoWindow = true });
        else if (OperatingSystem.IsLinux())
            Process.Start(new ProcessStartInfo
                { FileName = "xdg-open", Arguments = $"\"{url}\"", CreateNoWindow = true });
        else if (OperatingSystem.IsMacOS())
            Process.Start(new ProcessStartInfo { FileName = "open", Arguments = $"\"{url}\"", CreateNoWindow = true });
    }

    public static async Task<string?> ReceiveAuthCodeAsync()
    {
        // Запускаем локальный HTTP-сервер
        var listener = new HttpListener();
        listener.Prefixes.Add(CallbackUrl + "/");
        listener.Start();

        // Ждем входящий запрос
        var context = await listener.GetContextAsync();
        var request = context.Request;
        // Отправляем ответ клиенту
        var response = context.Response;
        var bytes = Encoding.UTF8.GetBytes("Code received. Close this page");
        response.ContentLength64 = bytes.Length;
        response.ContentEncoding = Encoding.UTF8;
        await using (var output = response.OutputStream)
        {
            await output.WriteAsync(bytes);
            output.Close();
        }

        listener.Stop();

        return request.QueryString.Get("code");
    }

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