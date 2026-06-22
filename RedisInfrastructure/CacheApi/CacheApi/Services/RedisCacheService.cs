using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace RedisCacheLab.Services;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null);
    Task<bool> RemoveAsync(string key);
    Task RemoveByPrefixAsync(string prefix);
    Task<bool> ExistsAsync(string key);
}

/// <summary>
/// Encapsula o IConnectionMultiplexer do StackExchange.Redis.
/// Centraliza serialização e tratamento de TTL para todas as estratégias.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    private IDatabase Db => _redis.GetDatabase();

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await Db.StringGetAsync(key);
        if (value.IsNullOrEmpty)
        {
            _logger.LogInformation("CACHE MISS -> {Key}", key);
            return default;
        }

        _logger.LogInformation("CACHE HIT  -> {Key}", key);
        return JsonSerializer.Deserialize<T>(value.ToString());
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null)
    {
        var json = JsonSerializer.Serialize(value);
        var expiry = ttl.HasValue ? new Expiration(ttl.Value) : default(Expiration);
        await Db.StringSetAsync(key, json, expiry);
        _logger.LogInformation("CACHE SET  -> {Key} (TTL: {Ttl})", key, ttl?.ToString() ?? "sem expiração");
    }

    public async Task<bool> RemoveAsync(string key)
    {
        _logger.LogInformation("CACHE DEL  -> {Key}", key);
        return await Db.KeyDeleteAsync(key);
    }

    public async Task RemoveByPrefixAsync(string prefix)
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var keys = server.Keys(pattern: $"{prefix}*").ToArray();
        if (keys.Length == 0) return;

        await Db.KeyDeleteAsync(keys);
        _logger.LogInformation("CACHE DEL  -> {Count} chaves com prefixo '{Prefix}'", keys.Length, prefix);
    }

    public async Task<bool> ExistsAsync(string key) => await Db.KeyExistsAsync(key);
}
