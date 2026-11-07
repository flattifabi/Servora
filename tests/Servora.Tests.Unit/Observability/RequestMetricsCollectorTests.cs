using Servora.Infrastructure.Observability.Metrics;

namespace Servora.Tests.Unit.Observability;

/// <summary>Tests for the per-minute request metrics aggregation.</summary>
public sealed class RequestMetricsCollectorTests
{
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 3, 1, 12, 0, 10, TimeSpan.Zero));

    [Fact]
    public void Snapshot_AggregatesPerMinute()
    {
        var collector = new RequestMetricsCollector(_clock);
        collector.Record("GET /a", 200, 10);
        collector.Record("GET /a", 404, 30);
        _clock.Advance(TimeSpan.FromMinutes(1));
        collector.Record("POST /b", 500, 200);

        var snapshot = collector.GetSnapshot(TimeSpan.FromMinutes(5));

        Assert.Equal(5, snapshot.Minutes.Count);
        Assert.Equal(3, snapshot.TotalRequests);
        Assert.Equal(1, snapshot.ClientErrors);
        Assert.Equal(1, snapshot.ServerErrors);
        Assert.Equal(80, snapshot.AverageMs, 3);
        Assert.Equal(200, snapshot.MaxMs);
        var previousMinute = snapshot.Minutes[^2];
        Assert.Equal(2, previousMinute.Count);
        Assert.Equal(20, previousMinute.AverageMs, 3);
        Assert.Equal(1, snapshot.Minutes[^1].Count);
        Assert.Equal(0, snapshot.Minutes[0].Count);
        Assert.Equal("GET /a", snapshot.TopRoutes[0].Route);
        Assert.Equal(1, snapshot.TopRoutes.Single(r => r.Route == "POST /b").Errors);
    }

    [Fact]
    public void OldMinutes_AreEvictedAfter24Hours()
    {
        var collector = new RequestMetricsCollector(_clock);
        collector.Record("GET /a", 200, 5);
        _clock.Advance(TimeSpan.FromHours(24) + TimeSpan.FromMinutes(1));
        collector.Record("GET /a", 200, 5);

        var snapshot = collector.GetSnapshot(TimeSpan.FromHours(24));

        Assert.Equal(RequestMetricsCollector.Capacity, snapshot.Minutes.Count);
        Assert.Equal(1, snapshot.TotalRequests);
    }

    [Fact]
    public void P95_UsesHistogramBuckets()
    {
        var collector = new RequestMetricsCollector(_clock);
        for (var i = 0; i < 95; i++)
        {
            collector.Record("GET /fast", 200, 4);
        }

        for (var i = 0; i < 5; i++)
        {
            collector.Record("GET /slow", 200, 900);
        }

        var snapshot = collector.GetSnapshot(TimeSpan.FromMinutes(1));

        Assert.Equal(5, snapshot.P95Ms); // upper bound of the 0-5 ms histogram bucket
        Assert.Equal(900, snapshot.MaxMs);
        Assert.Equal(100, snapshot.RequestsPerMinute);
    }

    [Fact]
    public void RouteCardinality_IsBounded()
    {
        var collector = new RequestMetricsCollector(_clock);
        for (var i = 0; i < RequestMetricsCollector.MaxRoutes + 50; i++)
        {
            collector.Record($"GET /r{i}", 200, 1);
        }

        var snapshot = collector.GetSnapshot(TimeSpan.FromMinutes(1));

        Assert.Equal(RequestMetricsCollector.MaxRoutes + 50, snapshot.TotalRequests);
        Assert.Equal(50, snapshot.TopRoutes.Single(r => r.Route == "(other)").Count);
    }
}
