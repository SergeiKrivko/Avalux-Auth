using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;

namespace AvaluxAuth.Services;

/// <summary>
/// Fail-closed sender used when no mail backend is configured (R-01 in
/// SECURITY-AUDIT.md). Recovery answers 503 instead of accepting a request that
/// cannot be delivered.
/// </summary>
public class DisabledEmailSender : IEmailSender
{
    public bool IsEnabled => false;

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        => throw new InvalidOperationException("E-mail delivery is disabled");
}
