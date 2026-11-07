namespace Servora.Application.Cmdb;

/// <summary>Records referencing a configuration item, grouped by availability.</summary>
/// <param name="Records">The records the caller may see.</param>
/// <param name="UnavailableTables">Labels of tables that could not be queried (module not installed or access denied).</param>
public sealed record CiRelatedRecords(IReadOnlyList<CiRelatedRecord> Records, IReadOnlyList<string> UnavailableTables);
