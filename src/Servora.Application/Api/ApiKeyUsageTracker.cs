using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Domain.Api;

namespace Servora.Application.Api;

/// <summary>
/// Singleton that aggregates API key usage in memory (successes, failures, last use) so authentication does not write
/// to the database on every request. A background service flushes the counters periodically.
/// </summary>
/// <param name="clock">The clock.</param>
public sealed class ApiKeyUsageTracker(IClock clock)
{
    private const int MaxBuckets = 50_000;
    private readonly object _sync = new();
    private Dictionary<(Guid KeyId, DateTimeOffset Day), ApiKeyUsageBucket> _buckets = [];

    /// <summary>Gets the number of buckets waiting to be flushed.</summary>
    public int PendingCount
    {
        get
        {
            lock (_sync)
            {
                return _buckets.Count;
            }
        }
    }

    /// <summary>Records a successfully authenticated request.</summary>
    /// <param name="apiKeyId">The key identifier.</param>
    /// <param name="ipAddress">The client address.</param>
    public void RecordSuccess(Guid apiKeyId, string? ipAddress)
    {
        var now = clock.UtcNow;
        lock (_sync)
        {
            var bucket = GetBucket(apiKeyId, now);
            bucket.Requests++;
            bucket.LastUsedAt = now;
            bucket.LastUsedIp = ipAddress is { Length: > 64 } ? ipAddress[..64] : ipAddress;
        }
    }

    /// <summary>Records a rejected attempt with a known key id (wrong secret, revoked, expired, IP not allowed).</summary>
    /// <param name="apiKeyId">The key identifier.</param>
    public void RecordFailure(Guid apiKeyId)
    {
        var now = clock.UtcNow;
        lock (_sync)
        {
            GetBucket(apiKeyId, now).Failed++;
        }
    }

    /// <summary>Takes all pending buckets.</summary>
    /// <returns>The buckets.</returns>
    public IReadOnlyList<ApiKeyUsageBucket> Drain()
    {
        lock (_sync)
        {
            var drained = _buckets.Values.ToList();
            _buckets = [];
            return drained;
        }
    }

    /// <summary>Writes pending counters to the database (daily usage rows and last-used fields of the keys).</summary>
    /// <param name="db">An open database context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of flushed buckets.</returns>
    public async Task<int> FlushAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var buckets = Drain();
        if (buckets.Count == 0)
        {
            return 0;
        }

        var keyIds = buckets.Select(b => b.ApiKeyId).Distinct().ToList();
        var days = buckets.Select(b => b.Day).Distinct().ToList();
        var existing = await db.Set<ApiKeyUsageDay>()
            .Where(u => keyIds.Contains(u.ApiKeyId) && days.Contains(u.Day))
            .ToListAsync(cancellationToken);

        foreach (var bucket in buckets)
        {
            var row = existing.FirstOrDefault(u => u.ApiKeyId == bucket.ApiKeyId && u.Day == bucket.Day);
            if (row is null)
            {
                row = new ApiKeyUsageDay { ApiKeyId = bucket.ApiKeyId, Day = bucket.Day };
                db.Set<ApiKeyUsageDay>().Add(row);
                existing.Add(row);
            }

            row.RequestCount += bucket.Requests;
            row.FailedCount += bucket.Failed;
        }

        await db.SaveChangesAsync(cancellationToken);

        // Last-used fields are updated set-based so that the key's audit fields and concurrency version stay untouched.
        foreach (var group in buckets.Where(b => b.Requests > 0).GroupBy(b => b.ApiKeyId))
        {
            var count = group.Sum(b => b.Requests);
            var last = group.OrderByDescending(b => b.LastUsedAt).First();
            var lastUsedAt = last.LastUsedAt;
            var lastUsedIp = last.LastUsedIp;
            await db.Set<ApiKey>()
                .Where(k => k.Id == group.Key)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(k => k.LastUsedAt, lastUsedAt)
                        .SetProperty(k => k.LastUsedIp, lastUsedIp)
                        .SetProperty(k => k.UsageCount, k => k.UsageCount + count),
                    cancellationToken);
        }

        return buckets.Count;
    }

    private ApiKeyUsageBucket GetBucket(Guid apiKeyId, DateTimeOffset now)
    {
        var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        if (!_buckets.TryGetValue((apiKeyId, day), out var bucket))
        {
            if (_buckets.Count >= MaxBuckets)
            {
                // Defensive bound: if flushing stalls, keep memory bounded by dropping the oldest counters.
                _buckets.Clear();
            }

            bucket = new ApiKeyUsageBucket { ApiKeyId = apiKeyId, Day = day };
            _buckets[(apiKeyId, day)] = bucket;
        }

        return bucket;
    }
}
