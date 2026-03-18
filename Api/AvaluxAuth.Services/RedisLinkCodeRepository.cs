using System.Text.Json;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using StackExchange.Redis;

namespace AvaluxAuth.Services;

public class RedisLinkCodeRepository(IConnectionMultiplexer redis) : ILinkCodeRepository
{
    private const string RedisKeyPrefix = "LinkCode-";

    public async Task SaveCodeAsync(LinkCode code)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(RedisKeyPrefix + code.Code, JsonSerializer.Serialize(code), TimeSpan.FromHours(1));
    }

    public async Task<LinkCode?> GetCodeAsync(string code)
    {
        var db = redis.GetDatabase();
        var result = await db.StringGetAsync(RedisKeyPrefix + code);
        return JsonSerializer.Deserialize<LinkCode>(result.ToString());
    }

    public async Task DeleteCodeAsync(string code)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(RedisKeyPrefix + code);
    }

    public async Task<LinkCode?> TakeCodeAsync(string code)
    {
        var db = redis.GetDatabase();
        var result = await db.StringGetAsync(RedisKeyPrefix + code);
        if (!result.HasValue)
            return null;
        await db.KeyDeleteAsync(RedisKeyPrefix + code);
        return JsonSerializer.Deserialize<LinkCode>(result.ToString());
    }
}