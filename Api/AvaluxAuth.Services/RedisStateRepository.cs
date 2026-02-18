using System.Text.Json;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using StackExchange.Redis;

namespace AvaluxAuth.Services;

public class RedisStateRepository(IConnectionMultiplexer redis) : IStateRepository
{
    private const string RedisKeyPrefix = "AuthorizationState-";

    public async Task SaveStateAsync(AuthorizationState state)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(RedisKeyPrefix + state.State, JsonSerializer.Serialize(state), TimeSpan.FromHours(1));
    }

    public async Task<AuthorizationState?> GetStateAsync(string state)
    {
        var db = redis.GetDatabase();
        var result = await db.StringGetAsync(RedisKeyPrefix + state);
        return JsonSerializer.Deserialize<AuthorizationState>(result.ToString());
    }

    public async Task DeleteStateAsync(string state)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(RedisKeyPrefix + state);
    }

    public async Task<AuthorizationState?> TakeStateAsync(string state)
    {
        var db = redis.GetDatabase();
        var result = await db.StringGetAsync(RedisKeyPrefix + state);
        if (!result.HasValue)
            return null;
        await db.KeyDeleteAsync(RedisKeyPrefix + state);
        return JsonSerializer.Deserialize<AuthorizationState>(result.ToString());
    }
}