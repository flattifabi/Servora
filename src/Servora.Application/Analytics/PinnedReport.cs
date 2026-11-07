namespace Servora.Application.Analytics;

/// <summary>A report on the personal dashboard with its current result.</summary>
/// <param name="Report">The report.</param>
/// <param name="Wide">Whether the tile spans the full width.</param>
/// <param name="Result">The result, when it could be computed.</param>
/// <param name="Error">The reason the result is missing (module not installed, no permission, …).</param>
public sealed record PinnedReport(ReportSummary Report, bool Wide, ReportResult? Result, string? Error);
