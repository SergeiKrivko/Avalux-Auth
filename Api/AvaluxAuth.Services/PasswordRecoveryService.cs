using System.Security.Cryptography;
using System.Text;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AvaluxAuth.Services;

/// <summary>
/// Self-service password recovery over e-mail (R-01 in SECURITY-AUDIT.md).
///
/// The request half never reveals whether an account exists: it always reports
/// the same result to the caller, skips accounts without an address, applies a
/// per-account delivery cooldown and only enqueues the letter. The completion
/// half consumes a single-use token atomically, revokes every session linked to
/// the credential and only then replaces the BCrypt hash.
/// </summary>
public class PasswordRecoveryService(
    IPasswordRepository passwordRepository,
    IAccountRepository accountRepository,
    IUserRepository userRepository,
    IPasswordResetTokenRepository resetTokenRepository,
    IEmailSender emailSender,
    IEmailQueue emailQueue,
    IConfiguration configuration,
    ILogger<PasswordRecoveryService> logger) : IPasswordRecoveryService
{
    private const int DefaultTokenLifetimeMinutes = 30;
    private const int MinTokenLifetimeMinutes = 5;
    private const int MaxTokenLifetimeMinutes = 120;

    // Matches the length of the reset token (32 random bytes, hex encoded).
    private const int TokenLength = 64;

    private readonly TimeSpan _tokenLifetime = ResolveTokenLifetime(configuration, logger);
    private readonly string? _linkBaseUrl = ResolveLinkBaseUrl(configuration, logger);

    public bool IsEnabled => emailSender.IsEnabled && _linkBaseUrl != null;

    public async Task<RecoveryRequestResult> RequestAsync(string loginOrEmail, string clientIp,
        CancellationToken ct = default)
    {
        if (!IsEnabled)
        {
            logger.LogWarning("Password recovery request rejected: recovery is not configured");
            return RecoveryRequestResult.Disabled;
        }

        var identifier = loginOrEmail.Trim();
        if (identifier.Length == 0)
            return RecoveryRequestResult.Accepted;

        try
        {
            var accounts = await ResolveAccountsAsync(identifier, ct);
            var outcomes = new List<string>(accounts.Count);

            foreach (var account in accounts)
            {
                if (string.IsNullOrWhiteSpace(account.Info.Email))
                {
                    outcomes.Add("no-email");
                    continue;
                }

                if (!await resetTokenRepository.TryReserveSendAsync(account.Id, ct))
                {
                    outcomes.Add("cooldown");
                    continue;
                }

                var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(TokenLength / 2));
                await resetTokenRepository.SaveAsync(HashToken(token), new PasswordResetToken
                {
                    PasswordId = account.Id,
                    CreatedAt = DateTimeOffset.UtcNow,
                }, _tokenLifetime);

                var minutes = (int)_tokenLifetime.TotalMinutes;
                var link = $"{_linkBaseUrl}/recover#token={token}";
                emailQueue.TryEnqueue(BuildResetEmail(account, link, minutes));
                outcomes.Add("queued");
            }

            // The identifier is personal data, so only a hash reaches the log.
            logger.LogInformation(
                "Password recovery requested: outcomes={Outcomes}, accounts={Accounts}, identifierHash={IdentifierHash}, ip={ClientIp}",
                string.Join(",", outcomes), accounts.Count, HashIdentifier(identifier), clientIp);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            // Redis or Postgres failed. The caller still gets the generic answer,
            // so an outage does not turn the endpoint into an existence oracle.
            logger.LogError(e, "Password recovery request failed: identifierHash={IdentifierHash}, ip={ClientIp}",
                HashIdentifier(identifier), clientIp);
        }

        return RecoveryRequestResult.Accepted;
    }

    public async Task<RecoveryCompleteOutcome> CompleteAsync(string token, string password, string clientIp,
        CancellationToken ct = default)
    {
        if (!IsValidToken(token))
            return new RecoveryCompleteOutcome(RecoveryCompleteResult.InvalidToken);

        // Validate before consuming: a rejected password must not burn the link.
        var policyError = PasswordPolicy.Validate(password);
        if (policyError != null)
            return new RecoveryCompleteOutcome(RecoveryCompleteResult.PolicyViolation, policyError);

        var resetToken = await resetTokenRepository.TakeAsync(HashToken(token));
        if (resetToken == null)
        {
            logger.LogWarning("Password reset rejected: invalid, expired or reused token, ip={ClientIp}", clientIp);
            return new RecoveryCompleteOutcome(RecoveryCompleteResult.InvalidToken);
        }

        var account = await passwordRepository.GetByIdAsync(resetToken.PasswordId, ct);
        if (account == null)
        {
            logger.LogWarning(
                "Password reset rejected: password account is gone, passwordId={PasswordId}, ip={ClientIp}",
                resetToken.PasswordId, clientIp);
            return new RecoveryCompleteOutcome(RecoveryCompleteResult.InvalidToken);
        }

        // Revoke before changing the hash: if the update fails afterwards the user
        // is logged out but the old password still works, which is the safe
        // direction. The opposite order would leave valid sessions behind.
        var userIds = await accountRepository.GetUserIdsByPasswordAccountAsync(resetToken.PasswordId, ct);
        foreach (var userId in userIds)
        {
            var revoked = await userRepository.RevokeSessionsAsync(userId, ct);
            logger.LogWarning("All sessions revoked: userId={UserId}, reason=password-reset, revoked={Revoked}",
                userId, revoked);
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        if (!await passwordRepository.ChangePasswordAsync(resetToken.PasswordId, passwordHash, ct))
            return new RecoveryCompleteOutcome(RecoveryCompleteResult.InvalidToken);

        NotifyPasswordChanged(account);

        logger.LogWarning(
            "Password reset completed: passwordId={PasswordId}, revokedUsers={RevokedUsers}, ip={ClientIp}",
            resetToken.PasswordId, userIds.Count, clientIp);
        return new RecoveryCompleteOutcome(RecoveryCompleteResult.Success);
    }

    private async Task<IReadOnlyList<PasswordUser>> ResolveAccountsAsync(string identifier, CancellationToken ct)
    {
        // An exact login always wins: a login may itself contain '@', and using
        // the e-mail lookup first would silently redirect the request.
        var byLogin = await passwordRepository.GetByLoginAsync(identifier, ct);
        if (byLogin != null)
            return [byLogin];

        if (!identifier.Contains('@'))
            return [];

        // Several accounts may share an address (there is no unique index on
        // Passwords.Email, see M-12); the repository bounds the fan-out.
        return await passwordRepository.GetByEmailAsync(identifier, ct);
    }

    private EmailMessage BuildResetEmail(PasswordUser account, string link, int minutes)
    {
        return new EmailMessage
        {
            To = account.Info.Email!,
            Subject = "Восстановление пароля Avalux Auth",
            TextBody = $"""
                Здравствуйте!

                Вы запросили восстановление пароля в Avalux Auth.
                Логин: {account.Login}
                Ссылка действует {minutes} минут и может быть использована один раз:
                {link}

                Если вы не запрашивали восстановление, просто проигнорируйте это письмо — пароль останется прежним.
                """,
        };
    }

    private void NotifyPasswordChanged(PasswordUser account)
    {
        if (string.IsNullOrWhiteSpace(account.Info.Email))
            return;

        emailQueue.TryEnqueue(new EmailMessage
        {
            To = account.Info.Email,
            Subject = "Пароль Avalux Auth изменён",
            TextBody = """
                Здравствуйте!

                Пароль вашего аккаунта Avalux Auth был изменён, все активные сессии завершены.
                Если это сделали не вы, немедленно свяжитесь с администратором сервиса.
                """,
        });
    }

    private static bool IsValidToken(string token)
    {
        // Reject anything but the exact lowercase hex form before touching Redis.
        return token.Length == TokenLength && token.All(char.IsAsciiHexDigitLower);
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static string HashIdentifier(string identifier)
    {
        var normalized = identifier.Trim().ToLowerInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..16];
    }

    private static TimeSpan ResolveTokenLifetime(IConfiguration configuration,
        ILogger<PasswordRecoveryService> logger)
    {
        if (!int.TryParse(configuration["PasswordRecovery:TokenLifetimeMinutes"], out var minutes))
            return TimeSpan.FromMinutes(DefaultTokenLifetimeMinutes);

        if (minutes is < MinTokenLifetimeMinutes or > MaxTokenLifetimeMinutes)
        {
            logger.LogWarning(
                "PasswordRecovery:TokenLifetimeMinutes={Minutes} is outside [{Min}, {Max}]; using {Default}",
                minutes, MinTokenLifetimeMinutes, MaxTokenLifetimeMinutes, DefaultTokenLifetimeMinutes);
            return TimeSpan.FromMinutes(DefaultTokenLifetimeMinutes);
        }

        return TimeSpan.FromMinutes(minutes);
    }

    private static string? ResolveLinkBaseUrl(IConfiguration configuration,
        ILogger<PasswordRecoveryService> logger)
    {
        // Never trust X-Forwarded-Host here: the API shares the `proxy` network
        // with other containers, and a forged host would put an attacker's
        // domain into the delivered link.
        var configured = configuration["PasswordRecovery:LinkBaseUrl"];
        if (string.IsNullOrWhiteSpace(configured))
            configured = configuration["Api:ApiUrl"];

        if (string.IsNullOrWhiteSpace(configured) ||
            !Uri.TryCreate(configured.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            logger.LogCritical(
                "PasswordRecovery:LinkBaseUrl (or Api:ApiUrl) is not an absolute http(s) URL; password recovery is disabled");
            return null;
        }

        return configured.Trim().TrimEnd('/');
    }
}
