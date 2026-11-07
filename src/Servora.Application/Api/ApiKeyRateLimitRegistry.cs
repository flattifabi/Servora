using System.Collections.Concurrent;

namespace Servora.Application.Api;

/// <summary>
/// Singleton map of key id → configured rate limit, filled on successful authentication. The rate limiter runs before
/// authentication in the pipeline, so it partitions requests by the (unverified) key id and looks the limit up here.
/// </summary>
public sealed class ApiKeyRateLimitRegistry
{
    private const int MaxEntries = 10_000;
    private readonly ConcurrentDictionary<string, int> _limits = new(StringComparer.Ordinal);

    /// <summary>Records the limit of a key.</summary>
    /// <param name="keyId">The public key id.</param>
    /// <param name="limitPerMinute">The limit.</param>
    public void Set(string keyId, int limitPerMinute)
    {
        if (_limits.Count >= MaxEntries && !_limits.ContainsKey(keyId))
        {
            _limits.Clear();
        }

        _limits[keyId] = limitPerMinute;
    }

    /// <summary>Gets the limit of a key.</summary>
    /// <param name="keyId">The public key id.</param>
    /// <returns>The limit, or <see langword="null"/> when unknown.</returns>
    public int? Get(string keyId) => _limits.TryGetValue(keyId, out var limit) ? limit : null;

    /// <summary>Forgets a key (after revocation or changes).</summary>
    /// <param name="keyId">The public key id.</param>
    public void Remove(string keyId) => _limits.TryRemove(keyId, out _);
}
