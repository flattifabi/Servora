namespace Servora.Web.Components.UI.Charts;

/// <summary>A labeled value for donut charts and breakdown lists.</summary>
/// <param name="Label">The label.</param>
/// <param name="Value">The value.</param>
/// <param name="Color">An optional color; defaults to the palette.</param>
public sealed record ChartSlice(string Label, double Value, string? Color = null);
