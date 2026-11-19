namespace AvaluxAuth.Abstractions;

public enum RecoveryRequestResult
{
    /// <summary>Handled. The answer is deliberately the same for every account state.</summary>
    Accepted,

    /// <summary>No usable mail backend or link base is configured.</summary>
    Disabled,
}

public enum RecoveryCompleteResult
{
    Success,
    InvalidToken,
    PolicyViolation,
}

public sealed record RecoveryCompleteOutcome(RecoveryCompleteResult Result, string? Error = null);

/// <summary>
/// Self-service password recovery over e-mail (R-01 in SECURITY-AUDIT.md): a
/// single-use link is sent to the address stored on the password account, and
/// redeeming it replaces the password and revokes every session of every user
/// linked to that credential.
/// </summary>
public interface IPasswordRecoveryService
{
    bool IsEnabled { get; }

    Task<RecoveryRequestResult> RequestAsync(string loginOrEmail, string clientIp,
        CancellationToken ct = default);

    Task<RecoveryCompleteOutcome> CompleteAsync(string token, string password, string clientIp,
        CancellationToken ct = default);
}
