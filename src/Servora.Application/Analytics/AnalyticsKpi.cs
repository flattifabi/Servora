namespace Servora.Application.Analytics;

/// <summary>A KPI tile of the executive dashboard.</summary>
/// <param name="Key">The stable key.</param>
/// <param name="Label">The label.</param>
/// <param name="Value">The formatted value ("—" when unavailable).</param>
/// <param name="Caption">A short caption.</param>
/// <param name="Delta">The change against the previous period in percent (null when unknown).</param>
/// <param name="InvertDelta">Whether an increase is bad.</param>
/// <param name="Trend">Sparkline values (oldest first).</param>
/// <param name="Tone">The tone: primary, success, warning, danger, info or accent.</param>
/// <param name="Icon">The icon name.</param>
/// <param name="Availability">Whether the KPI could be computed.</param>
public sealed record AnalyticsKpi(
    string Key,
    string Label,
    string Value,
    string? Caption,
    double? Delta,
    bool InvertDelta,
    IReadOnlyList<double>? Trend,
    string Tone,
    string Icon,
    AnalyticsAvailability Availability);
