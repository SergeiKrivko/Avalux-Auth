using System.Text.Json;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using StackExchange.Redis;

namespace AvaluxAuth.Services;

public class RedisLinkCodeRepository(IConnectionMultiplexer redis) : ILinkCodeRepository
{
    private const string RedisKeyPrefix = "LinkCode-";

    public async Task SaveCodeAsync(AuthCode code)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(RedisKeyPrefix + code.Code, JsonSerializer.Serialize(code), TimeSpan.FromHours(1));
    }

    public async Task<AuthCode?> GetCodeAsync(string code)
    {
        var db = redis.GetDatabase();
        var result = await db.StringGetAsync(RedisKeyPrefix + code);
        return JsonSerializer.Deserialize<AuthCode>(result.ToString());
    }

    public async Task DeleteCodeAsync(string code)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(RedisKeyPrefix + code);
    }

    public async Task<AuthCode?> TakeCodeAsync(string code)
    {
        var db = redis.GetDatabase();
        var result = await db.StringGetAsync(RedisKeyPrefix + code);
        if (!result.HasValue)
            return null;
        await db.KeyDeleteAsync(RedisKeyPrefix + code);
        return JsonSerializer.Deserialize<AuthCode>(result.ToString());
    }
}