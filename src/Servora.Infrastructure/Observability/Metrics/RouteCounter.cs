namespace Servora.Infrastructure.Observability.Metrics;

/// <summary>Mutable totals of one route (guarded by the collector's lock).</summary>
internal sealed class RouteCounter
{
    /// <summary>Gets or sets the request count.</summary>
    public long Count { get; set; }

    /// <summary>Gets or sets the number of 5xx responses.</summary>
    public long Errors { get; set; }

    /// <summary>Gets or sets the summed duration.</summary>
    public double TotalMs { get; set; }

    /// <summary>Gets or sets the maximum duration.</summary>
    public double MaxMs { get; set; }
}
