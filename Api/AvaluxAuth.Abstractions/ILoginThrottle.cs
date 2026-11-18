namespace AvaluxAuth.Abstractions;

/// <summary>
/// Counts failed authentication attempts per (client IP, login) pair and applies an
/// exponential lockout (H-06 in SECURITY-AUDIT.md). Counters live in the shared cache,
/// so they survive process restarts and are shared by every instance.
/// </summary>
public interface ILoginThrottle
{
    /// <returns>The remaining lockout, or <c>null</c> when an attempt is allowed.</returns>
    Task<TimeSpan?> GetRetryAfterAsync(string scope, string clientIp, string login,
        CancellationToken ct = default);

    /// <returns>The lockout applied by this failure, or <c>null</c> while below the threshold.</returns>
    Task<TimeSpan?> RegisterFailureAsync(string scope, string clientIp, string login,
        CancellationToken ct = default);

    /// <summary>Forgets the counters after a successful authentication.</summary>
    Task ResetAsync(string scope, string clientIp, string login, CancellationToken ct = default);
}
