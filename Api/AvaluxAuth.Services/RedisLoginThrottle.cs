using System.Security.Cryptography;
using System.Text;
using AvaluxAuth.Abstractions;
using StackExchange.Redis;

namespace AvaluxAuth.Services;

public class RedisLoginThrottle(IConnectionMultiplexer redis) : ILoginThrottle
{
    /// <summary>Consecutive failures after which the pair is locked out.</summary>
    public const int FailureLimit = 5;

    private const string RedisKeyPrefix = "LoginThrottle-";
    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan InitialLockout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxLockout = TimeSpan.FromMinutes(15);

    public async Task<TimeSpan?> GetRetryAfterAsync(string scope, string clientIp, string login,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var db = redis.GetDatabase();
        var ttl = await db.KeyTimeToLiveAsync(BlockKey(scope, clientIp, login));
        return ttl is { } remaining && remaining > TimeSpan.Zero ? remaining : null;
    }

    public async Task<TimeSpan?> RegisterFailureAsync(string scope, string clientIp, string login,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var db = redis.GetDatabase();
        var countKey = CountKey(scope, clientIp, login);
        var failures = await db.StringIncrementAsync(countKey);
        // HasNoExpiry also repairs a key left without a TTL if the process died
        // between INCR and EXPIRE.
        await db.KeyExpireAsync(countKey, FailureWindow, ExpireWhen.HasNoExpiry);

        var lockout = GetLockout(failures);
        if (lockout is null)
            return null;

        await db.StringSetAsync(BlockKey(scope, clientIp, login), failures, lockout.Value, When.Always);
        return lockout;
    }

    public async Task ResetAsync(string scope, string clientIp, string login, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(
        [
            CountKey(scope, clientIp, login),
            BlockKey(scope, clientIp, login),
        ]);
    }

    /// <summary>
    /// Exponential backoff: the first <see cref="FailureLimit"/> failures are free,
    /// then the lockout doubles from 30 s up to 15 min.
    /// </summary>
    public static TimeSpan? GetLockout(long consecutiveFailures)
    {
        if (consecutiveFailures < FailureLimit)
            return null;

        var step = Math.Min(consecutiveFailures - FailureLimit, 10);
        var seconds = Math.Min(InitialLockout.TotalSeconds * Math.Pow(2, step), MaxLockout.TotalSeconds);
        return TimeSpan.FromSeconds(seconds);
    }

    // The raw login is personally identifiable data, so the key only contains a
    // hash. The trailing slice keeps Redis key names reasonably short.
    private static string Bucket(string clientIp, string login)
    {
        var normalized = $"{clientIp.Trim()}\n{login.Trim().ToLowerInvariant()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..32];
    }

    private static string CountKey(string scope, string clientIp, string login)
        => $"{RedisKeyPrefix}{scope}-count-{Bucket(clientIp, login)}";

    private static string BlockKey(string scope, string clientIp, string login)
        => $"{RedisKeyPrefix}{scope}-block-{Bucket(clientIp, login)}";
}
