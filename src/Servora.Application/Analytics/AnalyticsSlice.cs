namespace Servora.Application.Analytics;

/// <summary>A labeled value of a breakdown (donut, ranking).</summary>
/// <param name="Label">The label.</param>
/// <param name="Value">The value.</param>
/// <param name="Color">An optional CSS color.</param>
public sealed record AnalyticsSlice(string Label, double Value, string? Color = null);
