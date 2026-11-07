namespace Servora.Application.Analytics;

/// <summary>
/// One projected row of a report or dashboard query: the raw group key and the raw value to aggregate. Only these two
/// columns are read from the database; grouping and aggregation happen in memory (provider neutral).
/// </summary>
public sealed class ReportProjection
{
    /// <summary>Gets or sets the raw group key (enum, string, Guid, date, boolean, number or null).</summary>
    public object? Key { get; set; }

    /// <summary>Gets or sets the raw value (number or date) or null.</summary>
    public object? Value { get; set; }
}
