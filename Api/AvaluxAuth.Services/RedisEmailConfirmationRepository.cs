using System.Text.Json;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using StackExchange.Redis;

namespace AvaluxAuth.Services;

/// <summary>
/// Redis storage for single-use e-mail confirmation tickets (R-02 in
/// SECURITY-AUDIT.md). Keys contain the SHA-256 of the token, never the token
/// itself, and carry a TTL so an unused link disappears on its own.
/// </summary>
public class RedisEmailConfirmationRepository(IConnectionMultiplexer redis) : IEmailConfirmationRepository
{
    private const string KeyPrefix = "EmailConfirmation-";

    public async Task SaveAsync(string tokenHash, EmailConfirmationTicket ticket, TimeSpan lifetime)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(KeyPrefix + tokenHash, JsonSerializer.Serialize(ticket), lifetime);
    }

    public async Task<EmailConfirmationTicket?> TakeAsync(string tokenHash)
    {
        var db = redis.GetDatabase();
        // GETDEL (Redis 6.2+): the read and the delete are one operation, so two
        // concurrent confirmations cannot both consume the same link.
        var value = await db.StringGetDeleteAsync(KeyPrefix + tokenHash);
        return value.HasValue ? JsonSerializer.Deserialize<EmailConfirmationTicket>(value.ToString()) : null;
    }
}
