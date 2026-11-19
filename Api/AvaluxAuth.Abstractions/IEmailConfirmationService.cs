using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

/// <summary>Why a confirmation link was issued; it only changes the letter text.</summary>
public enum EmailConfirmationReason
{
    /// <summary>The address was supplied during registration.</summary>
    SignUp,

    /// <summary>The address was replaced on the profile page.</summary>
    EmailChanged,
}

public enum EmailConfirmationResult
{
    Success,
    InvalidToken,
}

/// <summary>Details of a queued confirmation letter, echoed to the sign-up page.</summary>
public sealed record EmailConfirmationStarted(string Email, DateTimeOffset ExpiresAt);

/// <summary>
/// Outcome of redeeming a confirmation link. <see cref="RedirectUrl"/> is set
/// only when the link also completed the interrupted authorization.
/// </summary>
public sealed record EmailConfirmationOutcome(EmailConfirmationResult Result, string? RedirectUrl = null);

/// <summary>
/// Optional e-mail confirmation (R-02 in SECURITY-AUDIT.md): a single-use link is
/// sent to the address stored on the password account, and following it confirms
/// the address and — for a link issued at sign-up — finishes the authorization
/// the user started, without another password prompt.
/// </summary>
public interface IEmailConfirmationService
{
    bool IsEnabled { get; }

    /// <summary>
    /// Issues a single-use link and queues the letter. Returns <c>null</c> when
    /// no letter was queued (feature disabled, no address, queue full); callers
    /// treat that as "skip the confirmation page", because confirmation is
    /// optional and a mail outage must not fail registration.
    /// </summary>
    Task<EmailConfirmationStarted?> StartAsync(Guid passwordId, string login, string? email, Guid userId,
        EmailConfirmationReason reason, AuthorizationState? authorization, CancellationToken ct = default);

    Task<EmailConfirmationOutcome> ConfirmAsync(string token, string clientIp, CancellationToken ct = default);
}
