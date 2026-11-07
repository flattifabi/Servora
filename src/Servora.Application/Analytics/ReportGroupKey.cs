namespace Servora.Application.Analytics;

/// <summary>A formatted group key.</summary>
/// <param name="Key">The stable key.</param>
/// <param name="Label">The display label.</param>
/// <param name="SortKey">The chronological / natural sort key (dates, numbers) or null.</param>
public sealed record ReportGroupKey(string Key, string Label, IComparable? SortKey);
