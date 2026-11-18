using System.Text.Json;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using StackExchange.Redis;

namespace AvaluxAuth.Services;

public class RedisStateRepository(IConnectionMultiplexer redis) : IStateRepository
{
    private const string RedisKeyPrefix = "AuthorizationState-";
    private const string FailureKeySuffix = "-failed-attempts";

    public async Task SaveStateAsync(AuthorizationState state)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(RedisKeyPrefix + state.State, JsonSerializer.Serialize(state), TimeSpan.FromHours(1));
    }

    public async Task<AuthorizationState?> GetStateAsync(string state)
    {
        var db = redis.GetDatabase();
        var result = await db.StringGetAsync(RedisKeyPrefix + state);
        if (!result.HasValue)
            return null;
        return JsonSerializer.Deserialize<AuthorizationState>(result.ToString());
    }

    public async Task DeleteStateAsync(string state)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync([RedisKeyPrefix + state, FailureKey(state)]);
    }

    public async Task<AuthorizationState?> TakeStateAsync(string state)
    {
        var db = redis.GetDatabase();
        var result = await db.StringGetAsync(RedisKeyPrefix + state);
        if (!result.HasValue)
            return null;
        await db.KeyDeleteAsync([RedisKeyPrefix + state, FailureKey(state)]);
        return JsonSerializer.Deserialize<AuthorizationState>(result.ToString());
    }

    public async Task<long> RegisterFailureAsync(string state)
    {
        var db = redis.GetDatabase();
        var key = FailureKey(state);
        var failures = await db.StringIncrementAsync(key);
        await db.KeyExpireAsync(key, TimeSpan.FromHours(1), ExpireWhen.HasNoExpiry);
        return failures;
    }

    private static string FailureKey(string state) => RedisKeyPrefix + state + FailureKeySuffix;
}