namespace Servora.Application.Analytics;

/// <summary>One group of a report result.</summary>
/// <param name="Key">The stable group key (raw value as invariant string; empty for "(empty)", <c>__other</c> for "Other").</param>
/// <param name="Label">The display label.</param>
/// <param name="Value">The aggregated value.</param>
/// <param name="Count">The number of records in the group.</param>
public sealed record ReportRow(string Key, string Label, double Value, int Count);
