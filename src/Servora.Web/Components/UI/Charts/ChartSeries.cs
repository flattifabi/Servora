namespace Servora.Web.Components.UI.Charts;

/// <summary>A named series of values for line and bar charts.</summary>
/// <param name="Name">The series name shown in the legend.</param>
/// <param name="Values">The values, one per category / x label.</param>
/// <param name="Color">An optional color; defaults to the palette.</param>
public sealed record ChartSeries(string Name, IReadOnlyList<double> Values, string? Color = null);
