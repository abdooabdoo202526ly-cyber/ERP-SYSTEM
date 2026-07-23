using System.Collections;
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace ERPSystem.Host.Utilities;

/// <summary>
/// TenantCache -- wrapper around IMemoryCache with tenant-scoped keys.
/// v1.0.20: replaced reflection-based InvalidatePrefix with an explicit
/// key tracker. The previous approach failed silently in .NET 8/9 because
/// MemoryCache internals changed and the entries field was not enumerable.
/// Now every key added via GetOrCreateAsync is also tracked in a
/// thread-safe set, and InvalidatePrefix / InvalidateTenant iterate over
/// that set, which is fast and reliable.
/// </summary>
public interface ITenantCache
{
    /// <summary>Get cached value or create it. Returns the cached or fresh value.</summary>
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>Get cached value (or default if missing).</summary>
    T? Get<T>(string key);

    /// <summary>Remove a specific key.</summary>
    void Remove(string key);

    /// <summary>Remove all keys starting with prefix (used after writes).</summary>
    void InvalidatePrefix(string prefix);

    /// <summary>Clear all cached entries for a tenant.</summary>
    void InvalidateTenant(Guid tenantId);

    /// <summary>Clear ALL cached entries. Nuclear option.</summary>
    void InvalidateAll();
}

public class TenantCache : ITenantCache
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<TenantCache> _logger;

    // Thread-safe set of all known keys (v1.0.20 replacement for reflection)
    private readonly ConcurrentDictionary<string, byte> _trackedKeys = new();

    public TenantCache(IMemoryCache cache, ILogger<TenantCache> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            _logger.LogDebug("[Cache] HIT {Key}", key);
            return cached;
        }
        _logger.LogDebug("[Cache] MISS {Key} - fetching", key);
        var fresh = await factory();
        if (fresh is not null)
        {
            _cache.Set(key, fresh, ttl);
            _trackedKeys.TryAdd(key, 0); // track for invalidation
        }
        return fresh;
    }

    public T? Get<T>(string key)
    {
        return _cache.TryGetValue(key, out T? v) ? v : default;
    }

    public void Remove(string key)
    {
        _cache.Remove(key);
        _trackedKeys.TryRemove(key, out _);
        _logger.LogDebug("[Cache] REMOVE {Key}", key);
    }

    public void InvalidatePrefix(string prefix)
    {
        // Iterate tracked keys (much more reliable than reflection in .NET 9)
        var toRemove = _trackedKeys.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        foreach (var k in toRemove)
        {
            _cache.Remove(k);
            _trackedKeys.TryRemove(k, out _);
        }
        if (toRemove.Count > 0)
        {
            _logger.LogInformation("[Cache] INVALIDATE {Prefix}* ({Count} entries)", prefix, toRemove.Count);
        }
        else
        {
            _logger.LogDebug("[Cache] INVALIDATE {Prefix}* (0 entries)", prefix);
        }
    }

    public void InvalidateTenant(Guid tenantId)
    {
        InvalidatePrefix($"t:{tenantId:N}:");
    }

    public void InvalidateAll()
    {
        var all = _trackedKeys.Keys.ToList();
        foreach (var k in all)
        {
            _cache.Remove(k);
            _trackedKeys.TryRemove(k, out _);
        }
        _logger.LogInformation("[Cache] INVALIDATE ALL ({Count} entries)", all.Count);
    }
}
