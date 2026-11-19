using System.Security.Cryptography;
using System.Text;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;

namespace AvaluxAuth.Api;

/// <summary>
/// Drains the in-process mail queue (R-01 in SECURITY-AUDIT.md). Delivery is
/// deliberately outside the request: the recovery endpoint must answer in
/// constant time and must not fail when SMTP is temporarily unavailable.
/// </summary>
public class EmailDispatcher(IEmailQueue queue, IEmailSender sender, ILogger<EmailDispatcher> logger)
    : BackgroundService
{
    private const int MaxAttempts = 3;

    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(10),
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!sender.IsEnabled)
        {
            logger.LogWarning("E-mail delivery is disabled; recovery and confirmation letters will not be sent");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            EmailMessage message;
            try
            {
                message = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await SendWithRetriesAsync(message, stoppingToken);
        }
    }

    private async Task SendWithRetriesAsync(EmailMessage message, CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await sender.SendAsync(message, stoppingToken);
                logger.LogInformation("Email delivered: recipientHash={RecipientHash}, subject={Subject}",
                    HashRecipient(message.To), message.Subject);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutdown: the queued message is dropped, the user can request a
                // new link. Never log the letter here.
                return;
            }
            catch (Exception e)
            {
                if (attempt == MaxAttempts)
                {
                    logger.LogError(e,
                        "Email delivery failed after {Attempts} attempts: recipientHash={RecipientHash}, subject={Subject}",
                        MaxAttempts, HashRecipient(message.To), message.Subject);
                    return;
                }

                logger.LogWarning(e, "Email delivery attempt {Attempt} failed: recipientHash={RecipientHash}",
                    attempt, HashRecipient(message.To));
                try
                {
                    await Task.Delay(RetryDelays[attempt - 1], stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    // The address is personal data (M-05); only a short hash reaches the log.
    private static string HashRecipient(string address)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(address.Trim().ToLowerInvariant())))[..16];
}
