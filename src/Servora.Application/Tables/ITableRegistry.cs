namespace Servora.Application.Tables;

/// <summary>
/// Catalog of all platform tables: typed entities decorated with <c>[RecordTable]</c> (discovered by reflection) plus
/// custom tables and custom fields defined in the UI. Cached; call <see cref="InvalidateAsync"/> after metadata changes.
/// </summary>
public interface ITableRegistry
{
    /// <summary>Gets all tables.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tables ordered by label.</returns>
    Task<IReadOnlyList<TableDefinition>> GetTablesAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a table by technical name.</summary>
    /// <param name="tableName">The technical table name (case-insensitive).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The table or <see langword="null"/>.</returns>
    Task<TableDefinition?> GetTableAsync(string tableName, CancellationToken cancellationToken = default);

    /// <summary>Resolves the technical table name of a typed entity type.</summary>
    /// <param name="entityType">The CLR type.</param>
    /// <returns>The table name or <see langword="null"/> if the type is not a table.</returns>
    string? GetTableName(Type entityType);

    /// <summary>Clears cached metadata after custom tables or fields were changed.</summary>
    /// <returns>A task that completes when the cache is cleared.</returns>
    Task InvalidateAsync();
}
