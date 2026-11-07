using Servora.Application.Common;
using Servora.Application.Observability;

namespace Servora.Infrastructure.Observability.Metrics;

/// <summary>
/// Thread-safe in-memory <see cref="IRequestMetrics"/>: a ring of 1,440 per-minute buckets (24 hours) with latency
/// histograms plus totals for at most <see cref="MaxRoutes"/> route templates (further routes count as "(other)").
/// </summary>
/// <param name="clock">The clock.</param>
public sealed class RequestMetricsCollector(IClock clock) : IRequestMetrics
{
    /// <summary>Number of minutes kept.</summary>
    public const int Capacity = 1440;

    /// <summary>Maximum number of distinct routes tracked.</summary>
    public const int MaxRoutes = 200;

    private const string OtherRoute = "(other)";
    private readonly MinuteBucket[] _buckets = Enumerable.Range(0, Capacity).Select(_ => new MinuteBucket()).ToArray();
    private readonly Dictionary<string, RouteCounter> _routes = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    /// <inheritdoc />
    public void Record(string route, int statusCode, double elapsedMs)
    {
        elapsedMs = Math.Max(0, elapsedMs);
        var minute = clock.UtcNow.ToUnixTimeSeconds() / 60;
        lock (_sync)
        {
            var bucket = _buckets[minute % Capacity];
            if (bucket.MinuteIndex != minute)
            {
                bucket.Reset(minute);
            }

            bucket.Add(statusCode, elapsedMs);

            var key = string.IsNullOrWhiteSpace(route) ? OtherRoute : route;
            if (!_routes.TryGetValue(key, out var counter))
            {
                if (_routes.Count >= MaxRoutes)
                {
                    key = OtherRoute;
                }

                if (!_routes.TryGetValue(key, out counter))
                {
                    counter = new RouteCounter();
                    _routes[key] = counter;
                }
            }

            counter.Count++;
            counter.TotalMs += elapsedMs;
            counter.MaxMs = Math.Max(counter.MaxMs, elapsedMs);
            if (statusCode >= 500)
            {
                counter.Errors++;
            }
        }
    }

    /// <inheritdoc />
    public RequestMetricsSnapshot GetSnapshot(TimeSpan window)
    {
        var minutes = (int)Math.Clamp(Math.Ceiling(window.TotalMinutes), 1, Capacity);
        var now = clock.UtcNow.ToUnixTimeSeconds() / 60;
        var result = new List<MetricsMinute>(minutes);
        var histogram = new int[MinuteBucket.Bounds.Length + 1];
        long total = 0, clientErrors = 0, serverErrors = 0;
        double totalMs = 0, maxMs = 0;
        List<RouteMetrics> routes;

        lock (_sync)
        {
            for (var index = now - minutes + 1; index <= now; index++)
            {
                var timestamp = DateTimeOffset.FromUnixTimeSeconds(index * 60);
                var bucket = _buckets[((index % Capacity) + Capacity) % Capacity];
                if (bucket.MinuteIndex != index || bucket.Count == 0)
                {
                    result.Add(new MetricsMinute(timestamp, 0, 0, 0, 0, 0, 0));
                    continue;
                }

                result.Add(new MetricsMinute(
                    timestamp,
                    bucket.Count,
                    bucket.ClientErrors,
                    bucket.ServerErrors,
                    bucket.TotalMs / bucket.Count,
                    MinuteBucket.Percentile(bucket.Histogram, bucket.Count, bucket.MaxMs, 0.95),
                    bucket.MaxMs));
                total += bucket.Count;
                clientErrors += bucket.ClientErrors;
                serverErrors += bucket.ServerErrors;
                totalMs += bucket.TotalMs;
                maxMs = Math.Max(maxMs, bucket.MaxMs);
                for (var i = 0; i < histogram.Length; i++)
                {
                    histogram[i] += bucket.Histogram[i];
                }
            }

            routes = _routes
                .OrderByDescending(r => r.Value.Count)
                .Take(15)
                .Select(r => new RouteMetrics(r.Key, r.Value.Count, r.Value.Errors, r.Value.Count == 0 ? 0 : r.Value.TotalMs / r.Value.Count, r.Value.MaxMs))
                .ToList();
        }

        return new RequestMetricsSnapshot(
            minutes,
            total,
            clientErrors,
            serverErrors,
            total == 0 ? 0 : totalMs / total,
            MinuteBucket.Percentile(histogram, (int)Math.Min(total, int.MaxValue), maxMs, 0.95),
            maxMs,
            result,
            routes);
    }
}
