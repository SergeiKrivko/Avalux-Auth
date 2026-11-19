using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace AvaluxAuth.Services;

/// <summary>
/// SMTP delivery of transactional mail (R-01 in SECURITY-AUDIT.md). A new
/// connection is opened per letter: the volume is low, and a long-lived client
/// would need its own reconnect logic.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private const int DefaultPort = 587;
    private const int DefaultTimeoutSeconds = 15;

    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly string? _host;
    private readonly int _port;
    private readonly SecureSocketOptions _security;
    private readonly string? _user;
    private readonly string? _password;
    private readonly string? _fromAddress;
    private readonly string _fromName;
    private readonly int _timeoutSeconds;

    public bool IsEnabled { get; }

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _logger = logger;
        _host = configuration["Email:Host"]?.Trim();
        _port = ParsePort(configuration["Email:Port"]);
        _security = ParseSecurity(configuration["Email:Security"]);
        _user = configuration["Email:User"];
        _password = configuration["Email:Password"];
        _fromAddress = configuration["Email:FromAddress"]?.Trim();
        _fromName = configuration["Email:FromName"]?.Trim() is { Length: > 0 } name ? name : "Avalux Auth";
        _timeoutSeconds = ParseTimeout(configuration["Email:TimeoutSeconds"]);

        IsEnabled = !string.IsNullOrWhiteSpace(_host) && IsValidFromAddress(_fromAddress);
        if (!IsEnabled)
        {
            // Fail closed without taking the whole auth service down: the
            // operator still needs login to work while fixing the SMTP config.
            logger.LogCritical(
                "SMTP e-mail is not configured (Email:Mode=Smtp but Email:Host/Email:FromAddress is missing or invalid); password recovery is disabled");
        }
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (!IsEnabled)
            throw new InvalidOperationException("SMTP is not configured");

        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(_fromName, _fromAddress!));
        mimeMessage.To.Add(MailboxAddress.Parse(message.To));
        mimeMessage.Subject = message.Subject;
        mimeMessage.Body = new TextPart("plain") { Text = message.TextBody };

        using var client = new SmtpClient { Timeout = _timeoutSeconds * 1000 };
        await client.ConnectAsync(_host!, _port, _security, ct);
        if (!string.IsNullOrEmpty(_user))
            await client.AuthenticateAsync(_user, _password ?? string.Empty, ct);
        await client.SendAsync(mimeMessage, ct);
        await client.DisconnectAsync(true, ct);
    }

    private static int ParsePort(string? value)
    {
        return int.TryParse(value, out var port) && port is > 0 and <= 65535 ? port : DefaultPort;
    }

    private static int ParseTimeout(string? value)
    {
        return int.TryParse(value, out var seconds) && seconds is > 0 and <= 120
            ? seconds
            : DefaultTimeoutSeconds;
    }

    private static SecureSocketOptions ParseSecurity(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "sslonconnect" or "ssl" => SecureSocketOptions.SslOnConnect,
            "auto" => SecureSocketOptions.Auto,
            _ => SecureSocketOptions.StartTls,
        };
    }

    private static bool IsValidFromAddress(string? address)
    {
        return !string.IsNullOrWhiteSpace(address) &&
               MailboxAddress.TryParse(address, out var mailbox) &&
               mailbox is not null &&
               string.IsNullOrEmpty(mailbox.Name) &&
               string.Equals(mailbox.Address, address, StringComparison.Ordinal);
    }
}
