using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

/// <summary>
/// Bounded in-process queue between a request and <c>EmailDispatcher</c>: an SMTP
/// dialogue must not delay (or reveal the existence of an account through the
/// duration of) the password recovery request.
/// </summary>
public interface IEmailQueue
{
    bool TryEnqueue(EmailMessage message);

    ValueTask<EmailMessage> DequeueAsync(CancellationToken ct = default);
}
