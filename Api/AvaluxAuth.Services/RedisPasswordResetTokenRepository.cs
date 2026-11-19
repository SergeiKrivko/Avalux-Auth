using System.Text.Json;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using StackExchange.Redis;

namespace AvaluxAuth.Services;

/// <summary>
/// Redis storage for one-time password reset tokens (R-01 in SECURITY-AUDIT.md).
/// Keys contain the SHA-256 of the token, never the token itself, and carry a
/// TTL so an unused link disappears on its own.
/// </summary>
public class RedisPasswordResetTokenRepository(IConnectionMultiplexer redis) : IPasswordResetTokenRepository
{
    private const string KeyPrefix = "PasswordReset-";
    private const string CooldownPrefix = "PasswordResetCooldown-";
    private const string HourlyPrefix = "PasswordResetHourly-";

    // One letter per account per minute, and no more than five per hour: the
    // endpoint must not be usable as a way to flood somebody else's mailbox.
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan HourlyWindow = TimeSpan.FromHours(1);
    private const int HourlyLimit = 5;

    public async Task SaveAsync(string tokenHash, PasswordResetToken token, TimeSpan lifetime)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(KeyPrefix + tokenHash, JsonSerializer.Serialize(token), lifetime);
    }

    public async Task<PasswordResetToken?> TakeAsync(string tokenHash)
    {
        var db = redis.GetDatabase();
        // GETDEL (Redis 6.2+): the read and the delete are one operation, so two
        // concurrent completions cannot both consume the same token.
        var value = await db.StringGetDeleteAsync(KeyPrefix + tokenHash);
        if (!value.HasValue)
            return null;
        return JsonSerializer.Deserialize<PasswordResetToken>(value.ToString());
    }

    public async Task<bool> TryReserveSendAsync(Guid passwordId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var db = redis.GetDatabase();
        // The password id is an internal GUID, not personal data.
        var id = passwordId.ToString("N");

        var cooldownSet = await db.StringSetAsync(CooldownPrefix + id, 1, Cooldown, When.NotExists);
        if (!cooldownSet)
            return false;

        var hourlyKey = HourlyPrefix + id;
        var sent = await db.StringIncrementAsync(hourlyKey);
        // HasNoExpiry also repairs a key left without a TTL if the process died
        // between INCR and EXPIRE.
        await db.KeyExpireAsync(hourlyKey, HourlyWindow, ExpireWhen.HasNoExpiry);
        return sent <= HourlyLimit;
    }
}
