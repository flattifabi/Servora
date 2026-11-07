namespace Servora.Domain.Common;

/// <summary>
/// Implemented by entities whose table name is only known at runtime (records of administrator-defined custom tables).
/// The persistence layer uses <see cref="TableName"/> instead of <see cref="RecordTableAttribute"/> to raise record events.
/// </summary>
public interface IDynamicTableRecord
{
    /// <summary>Gets the technical table name of the record.</summary>
    string TableName { get; }

    /// <summary>Gets the field values of the record for event snapshots and conditions.</summary>
    /// <returns>The field values keyed by field name.</returns>
    IReadOnlyDictionary<string, object?> GetFieldValues();
}
