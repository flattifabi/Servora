namespace Servora.Application.Tables;

/// <summary>Evaluates table level permissions (module permission or <c>table.&lt;name&gt;.*</c>) for the current actor.</summary>
public interface ITableAccessService
{
    /// <summary>Checks whether the current actor may perform an operation on a table.</summary>
    /// <param name="tableName">The technical table name.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    Task<bool> IsAllowedAsync(string tableName, TableOperation operation, CancellationToken cancellationToken = default);

    /// <summary>Checks whether a field may be written through generic record access.</summary>
    /// <param name="table">The table.</param>
    /// <param name="field">The field.</param>
    /// <returns><see langword="true"/> when writable.</returns>
    bool IsFieldWritable(TableDefinition table, FieldDefinition field);
}
