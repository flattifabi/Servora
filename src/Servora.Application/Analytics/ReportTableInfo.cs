using Servora.Application.Tables;

namespace Servora.Application.Analytics;

/// <summary>A table the current user can report on, with the fields usable in the report builder.</summary>
/// <param name="Table">The table definition (all exposed fields, for filters).</param>
/// <param name="GroupableFields">Fields usable for grouping.</param>
/// <param name="NumericFields">Fields usable for sum / average / min / max.</param>
public sealed record ReportTableInfo(TableDefinition Table, IReadOnlyList<FieldDefinition> GroupableFields, IReadOnlyList<FieldDefinition> NumericFields);
