using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

/// <summary>
/// Outbound transactional e-mail. <see cref="IsEnabled"/> is false when the
/// deployment has no usable mail backend; password recovery is then disabled
/// (fail-closed) instead of pretending to send a letter that never arrives.
/// </summary>
public interface IEmailSender
{
    bool IsEnabled { get; }

    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
