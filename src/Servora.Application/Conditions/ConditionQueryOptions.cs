using Servora.Application.Tables;
using Servora.Domain.Platform;

namespace Servora.Application.Conditions;

/// <summary>
/// Extra information for translating conditions on fields that are not CLR properties (custom table columns and custom
/// fields of typed tables). Those fields are matched through the <see cref="RecordFieldValue"/> index with correlated
/// <c>EXISTS</c> subqueries, which every supported provider executes.
/// </summary>
public sealed class ConditionQueryOptions
{
    /// <summary>Gets the technical table name used to scope the index rows.</summary>
    public required string TableName { get; init; }

    /// <summary>Gets the index set of the same database context as the queried entity set.</summary>
    public required IQueryable<RecordFieldValue> FieldValues { get; init; }

    /// <summary>Gets a resolver returning the definition of an indexed (non-property) field, or <see langword="null"/>.</summary>
    public required Func<string, FieldDefinition?> ResolveIndexedField { get; init; }
}
