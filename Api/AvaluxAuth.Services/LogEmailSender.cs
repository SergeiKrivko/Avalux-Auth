using System.Security.Cryptography;
using System.Text;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using Microsoft.Extensions.Logging;

namespace AvaluxAuth.Services;

/// <summary>
/// Development-only sender: writes the letter, including the one-time recovery
/// link, to the log so the flow can be tested on a disposable stand without an
/// SMTP relay (R-01 in SECURITY-AUDIT.md). Never enable it on a live deployment.
/// </summary>
public class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public bool IsEnabled => true;

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        logger.LogWarning(
            "Email (Log mode, reset links are written to the log): toHash={ToHash}, subject={Subject}, body={Body}",
            HashAddress(message.To), message.Subject, message.TextBody);
        return Task.CompletedTask;
    }

    private static string HashAddress(string address)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(address.Trim().ToLowerInvariant())))[..16];
}
