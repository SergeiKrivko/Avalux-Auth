using System.Security.Cryptography;
using System.Text;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using AvaluxAuth.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AvaluxAuth.Services;

/// <summary>
/// Optional e-mail confirmation (R-02 in SECURITY-AUDIT.md).
///
/// Registration queues a single-use link. Following it marks the stored address
/// as confirmed and, because the interrupted authorization is kept with the
/// ticket, completes that authorization with a fresh code — the user is not asked
/// to sign in again. Confirmation is deliberately optional: if no mail backend is
/// configured or the link is never used, the account still works, it is just
/// flagged as unconfirmed.
/// </summary>
public class EmailConfirmationService(
    IPasswordRepository passwordRepository,
    IUserRepository userRepository,
    IAuthorizationService authorizationService,
    IEmailConfirmationRepository confirmationRepository,
    IEmailSender emailSender,
    IEmailQueue emailQueue,
    IConfiguration configuration,
    ILogger<EmailConfirmationService> logger) : IEmailConfirmationService
{
    private const int DefaultTokenLifetimeMinutes = 24 * 60;
    private const int MinTokenLifetimeMinutes = 15;
    private const int MaxTokenLifetimeMinutes = 7 * 24 * 60;

    // 32 random bytes, hex encoded: the same shape as a password reset token.
    private const int TokenLength = 64;

    private readonly TimeSpan _tokenLifetime = ResolveTokenLifetime(configuration, logger);
    private readonly string? _linkBaseUrl = ResolveLinkBaseUrl(configuration, logger);

    public bool IsEnabled => emailSender.IsEnabled && _linkBaseUrl != null;

    public async Task<EmailConfirmationStarted?> StartAsync(Guid passwordId, string login, string? email,
        Guid userId, EmailConfirmationReason reason, AuthorizationState? authorization,
        CancellationToken ct = default)
    {
        if (!IsEnabled)
            return null;

        var address = email?.Trim();
        if (string.IsNullOrEmpty(address))
            return null;

        try
        {
            var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(TokenLength / 2));
            var createdAt = DateTimeOffset.UtcNow;
            var expiresAt = createdAt.Add(_tokenLifetime);

            await confirmationRepository.SaveAsync(HashToken(token), new EmailConfirmationTicket
            {
                PasswordId = passwordId,
                UserId = userId,
                Email = address,
                CreatedAt = createdAt,
                Authorization = BuildPendingAuthorization(authorization),
            }, _tokenLifetime);

            var link = $"{_linkBaseUrl}/confirm#token={token}";
            if (!emailQueue.TryEnqueue(BuildEmail(login, address, link, reason)))
                return null;

            logger.LogInformation(
                "E-mail confirmation queued: reason={Reason}, passwordId={PasswordId}, userId={UserId}, hasAuthorization={HasAuthorization}",
                reason, passwordId, userId, authorization != null);
            return new EmailConfirmationStarted(address, expiresAt);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            // Confirmation is optional: a Redis or queue failure must not fail a
            // registration that already succeeded. The page is simply not shown.
            logger.LogError(e,
                "E-mail confirmation could not be started: reason={Reason}, userId={UserId}", reason, userId);
            return null;
        }
    }

    public async Task<EmailConfirmationOutcome> ConfirmAsync(string token, string clientIp,
        CancellationToken ct = default)
    {
        if (!IsValidToken(token))
            return new EmailConfirmationOutcome(EmailConfirmationResult.InvalidToken);

        var ticket = await confirmationRepository.TakeAsync(HashToken(token));
        if (ticket == null)
        {
            logger.LogWarning("E-mail confirmation rejected: invalid, expired or reused link, ip={ClientIp}",
                clientIp);
            return new EmailConfirmationOutcome(EmailConfirmationResult.InvalidToken);
        }

        // The address may have been edited between the letter and the click; a
        // stale link must not mark another address as confirmed.
        var account = await passwordRepository.GetByIdAsync(ticket.PasswordId, ct);
        if (account == null || !SameAddress(account.Info.Email, ticket.Email))
        {
            logger.LogWarning(
                "E-mail confirmation rejected: the address changed or the account is gone, passwordId={PasswordId}, ip={ClientIp}",
                ticket.PasswordId, clientIp);
            return new EmailConfirmationOutcome(EmailConfirmationResult.InvalidToken);
        }

        // The repository re-checks the address inside the UPDATE, so a profile
        // edit that races with this request cannot confirm the new address.
        if (!await passwordRepository.MarkEmailConfirmedAsync(ticket.PasswordId, ticket.Email, DateTime.UtcNow,
                ct))
            return new EmailConfirmationOutcome(EmailConfirmationResult.InvalidToken);

        var redirectUrl = ticket.Authorization == null
            ? null
            : await TryCompleteAuthorizationAsync(ticket, ct);

        logger.LogInformation(
            "E-mail confirmed: passwordId={PasswordId}, userId={UserId}, authorizationCompleted={AuthorizationCompleted}, ip={ClientIp}",
            ticket.PasswordId, ticket.UserId, redirectUrl != null, clientIp);
        return new EmailConfirmationOutcome(EmailConfirmationResult.Success, redirectUrl);
    }

    /// <summary>
    /// Mints a fresh authorization code for the request that registration
    /// interrupted. The code is issued now, so it carries the current session
    /// version (M-03) even if the password was changed in between.
    /// </summary>
    private async Task<string?> TryCompleteAuthorizationAsync(EmailConfirmationTicket ticket, CancellationToken ct)
    {
        var authorization = ticket.Authorization!;

        // The user may have been deleted after the letter was queued; a code for
        // a missing user would only fail later at the token endpoint.
        if (await userRepository.GetSessionVersionAsync(ticket.UserId, ct) == null)
        {
            logger.LogWarning(
                "Authorization not completed after e-mail confirmation: user is gone, userId={UserId}",
                ticket.UserId);
            return null;
        }

        var code = await authorizationService.CreateAuthorizationCodeAsync(ticket.UserId, authorization.ClientId,
            authorization.RedirectUrl, authorization.UserNonce, authorization.CodeChallenge,
            authorization.CodeChallengeMethod, ct);

        var builder = new UrlBuilder(authorization.RedirectUrl).AddQuery("code", code);
        if (authorization.UserState != null)
            builder.AddQuery("state", authorization.UserState);
        return builder.ToString();
    }

    private static EmailConfirmationAuthorization? BuildPendingAuthorization(AuthorizationState? authorization)
    {
        // The client id is what binds the eventual code to the requesting
        // application; without it the link can only confirm the address.
        if (authorization?.ClientId == null)
            return null;

        return new EmailConfirmationAuthorization
        {
            ClientId = authorization.ClientId,
            RedirectUrl = authorization.RedirectUrl,
            UserState = authorization.UserState,
            UserNonce = authorization.UserNonce,
            CodeChallenge = authorization.CodeChallenge,
            CodeChallengeMethod = authorization.CodeChallengeMethod,
        };
    }

    private EmailMessage BuildEmail(string login, string email, string link,
        EmailConfirmationReason reason)
    {
        var lifetime = DescribeLifetime((int)_tokenLifetime.TotalMinutes);

        if (reason == EmailConfirmationReason.SignUp)
        {
            return new EmailMessage
            {
                To = email,
                Subject = "Подтверждение email в Avalux Auth",
                TextBody = $"""
                    Здравствуйте!

                    Вы создали аккаунт Avalux Auth.
                    Логин: {login}

                    Подтвердите адрес электронной почты по ссылке:
                    {link}

                    Ссылка действует {lifetime} и может быть использована один раз.
                    После перехода авторизация завершится автоматически — входить заново не нужно.
                    Подтверждение необязательно: приложением можно пользоваться и без него.

                    Если вы не регистрировались, просто проигнорируйте это письмо.
                    """,
            };
        }

        return new EmailMessage
        {
            To = email,
            Subject = "Новый адрес почты в Avalux Auth",
            TextBody = $"""
                Здравствуйте!

                Адрес электронной почты в аккаунте Avalux Auth изменён.
                Логин: {login}

                Подтвердите новый адрес по ссылке:
                {link}

                Ссылка действует {lifetime} и может быть использована один раз.
                Если вы не меняли адрес, свяжитесь с администратором сервиса.
                """,
        };
    }

    private static string DescribeLifetime(int minutes)
    {
        if (minutes > 0 && minutes % (24 * 60) == 0)
        {
            var days = minutes / (24 * 60);
            return days == 1 ? "24 часа" : $"{days} суток";
        }

        return minutes >= 120 ? $"{minutes / 60} ч" : $"{Math.Max(1, minutes)} мин";
    }

    private static bool SameAddress(string? stored, string expected)
    {
        return string.Equals(stored?.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);
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

    private static TimeSpan ResolveTokenLifetime(IConfiguration configuration,
        ILogger<EmailConfirmationService> logger)
    {
        if (!int.TryParse(configuration["EmailConfirmation:TokenLifetimeMinutes"], out var minutes))
            return TimeSpan.FromMinutes(DefaultTokenLifetimeMinutes);

        if (minutes is < MinTokenLifetimeMinutes or > MaxTokenLifetimeMinutes)
        {
            logger.LogWarning(
                "EmailConfirmation:TokenLifetimeMinutes={Minutes} is outside [{Min}, {Max}]; using {Default}",
                minutes, MinTokenLifetimeMinutes, MaxTokenLifetimeMinutes, DefaultTokenLifetimeMinutes);
            return TimeSpan.FromMinutes(DefaultTokenLifetimeMinutes);
        }

        return TimeSpan.FromMinutes(minutes);
    }

    private static string? ResolveLinkBaseUrl(IConfiguration configuration,
        ILogger<EmailConfirmationService> logger)
    {
        // Never trust X-Forwarded-Host here: the API shares the `proxy` network
        // with other containers, and a forged host would put an attacker's
        // domain into the delivered link. The auth UI is served from the same
        // base URL as the API, so the recovery setting is a sane fallback.
        var configured = configuration["EmailConfirmation:LinkBaseUrl"];
        if (string.IsNullOrWhiteSpace(configured))
            configured = configuration["PasswordRecovery:LinkBaseUrl"];
        if (string.IsNullOrWhiteSpace(configured))
            configured = configuration["Api:ApiUrl"];

        if (string.IsNullOrWhiteSpace(configured) ||
            !Uri.TryCreate(configured.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            logger.LogCritical(
                "EmailConfirmation:LinkBaseUrl (or PasswordRecovery:LinkBaseUrl / Api:ApiUrl) is not an absolute http(s) URL; e-mail confirmation links are disabled");
            return null;
        }

        return configured.Trim().TrimEnd('/');
    }
}
