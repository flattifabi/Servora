namespace Servora.Application.Analytics;

/// <summary>A named series of values (one per label of the chart).</summary>
/// <param name="Name">The series name.</param>
/// <param name="Values">The values.</param>
/// <param name="Color">An optional CSS color.</param>
public sealed record AnalyticsSeries(string Name, IReadOnlyList<double> Values, string? Color = null);
