using System.Threading.Channels;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using Microsoft.Extensions.Logging;

namespace AvaluxAuth.Services;

/// <summary>
/// Bounded in-process mail queue (R-01 in SECURITY-AUDIT.md). The request path
/// only enqueues: an SMTP dialogue neither delays the generic recovery response
/// nor makes the response time depend on whether the account exists.
/// </summary>
public class EmailQueue(ILogger<EmailQueue> logger) : IEmailQueue
{
    private const int Capacity = 100;

    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });

    public bool TryEnqueue(EmailMessage message)
    {
        if (_channel.Writer.TryWrite(message))
            return true;

        // Dropping is not a client-visible error: a full queue must not turn the
        // recovery endpoint into an oracle or a 500.
        logger.LogWarning("Email queue is full; dropping a message with subject={Subject}", message.Subject);
        return false;
    }

    public ValueTask<EmailMessage> DequeueAsync(CancellationToken ct = default)
        => _channel.Reader.ReadAsync(ct);
}
