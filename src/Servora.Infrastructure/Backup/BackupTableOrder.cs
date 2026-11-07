namespace Servora.Infrastructure.Backup;

/// <summary>Result of <see cref="BackupTableOrdering.Compute"/>.</summary>
/// <param name="InsertOrder">The tables so that every principal precedes its dependents (except deferred foreign keys).</param>
/// <param name="DeferredColumns">
/// Per table key, the nullable foreign key columns that are written in a second pass (self references and foreign keys
/// that close a cycle). They are set to null before rows are deleted and inserted as null, then updated.
/// </param>
public sealed record BackupTableOrder(IReadOnlyList<BackupTableManifest> InsertOrder, IReadOnlyDictionary<string, IReadOnlySet<string>> DeferredColumns)
{
    /// <summary>Gets the deferred columns of a table.</summary>
    /// <param name="table">The table.</param>
    /// <returns>The deferred column names (possibly empty).</returns>
    public IReadOnlySet<string> GetDeferred(BackupTableManifest table) =>
        DeferredColumns.TryGetValue(table.Key, out var columns) ? columns : new HashSet<string>();
}
