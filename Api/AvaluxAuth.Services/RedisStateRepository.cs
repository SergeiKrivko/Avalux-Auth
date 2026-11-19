using System.Text.Json;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using StackExchange.Redis;

namespace AvaluxAuth.Services;

public class RedisStateRepository(IConnectionMultiplexer redis) : IStateRepository
{
    private const string RedisKeyPrefix = "AuthorizationState-";
    private const string FailureKeySuffix = "-failed-attempts";
    private const string UploadKeySuffix = "-avatar-uploads";

    // Matches the TTL of the state itself: counters die with the state.
    private static readonly TimeSpan StateTtl = TimeSpan.FromHours(1);

    public async Task SaveStateAsync(AuthorizationState state)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(RedisKeyPrefix + state.State, JsonSerializer.Serialize(state), StateTtl);
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
        await db.KeyDeleteAsync([RedisKeyPrefix + state, FailureKey(state), UploadKey(state)]);
    }

    public async Task<AuthorizationState?> TakeStateAsync(string state)
    {
        var db = redis.GetDatabase();
        var result = await db.StringGetAsync(RedisKeyPrefix + state);
        if (!result.HasValue)
            return null;
        await db.KeyDeleteAsync([RedisKeyPrefix + state, FailureKey(state), UploadKey(state)]);
        return JsonSerializer.Deserialize<AuthorizationState>(result.ToString());
    }

    public async Task<long> RegisterFailureAsync(string state)
    {
        var db = redis.GetDatabase();
        var key = FailureKey(state);
        var failures = await db.StringIncrementAsync(key);
        await db.KeyExpireAsync(key, StateTtl, ExpireWhen.HasNoExpiry);
        return failures;
    }

    public async Task<bool> TryRegisterUploadAsync(string state, int limit)
    {
        var db = redis.GetDatabase();
        var key = UploadKey(state);
        var uploads = await db.StringIncrementAsync(key);
        // HasNoExpiry also repairs a key left without a TTL (e.g. process died
        // between INCR and EXPIRE).
        await db.KeyExpireAsync(key, StateTtl, ExpireWhen.HasNoExpiry);
        return uploads <= limit;
    }

    private static string FailureKey(string state) => RedisKeyPrefix + state + FailureKeySuffix;

    private static string UploadKey(string state) => RedisKeyPrefix + state + UploadKeySuffix;
}