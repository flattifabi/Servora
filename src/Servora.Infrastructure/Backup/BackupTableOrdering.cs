namespace Servora.Infrastructure.Backup;

/// <summary>
/// Orders tables for restore (topological sort over foreign keys). Nullable self references are always deferred to a
/// second update pass; a cycle between tables is broken by deferring a nullable foreign key of the cycle. This lets a
/// restore run with ordinary permissions — no disabling of constraints, no superuser rights.
/// </summary>
public static class BackupTableOrdering
{
    /// <summary>Computes the insert order and the deferred foreign key columns.</summary>
    /// <param name="tables">The tables.</param>
    /// <returns>The order.</returns>
    /// <exception cref="BackupException">Thrown when tables form a cycle of non-nullable foreign keys.</exception>
    public static BackupTableOrder Compute(IReadOnlyList<BackupTableManifest> tables)
    {
        var byKey = new Dictionary<string, BackupTableManifest>(StringComparer.Ordinal);
        foreach (var table in tables)
        {
            byKey.TryAdd(table.Key, table);
        }

        var deferred = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var edges = new List<(BackupTableManifest Dependent, BackupTableManifest Principal, BackupForeignKeyManifest ForeignKey)>();
        foreach (var table in byKey.Values)
        {
            foreach (var foreignKey in table.ForeignKeys)
            {
                if (!byKey.TryGetValue(foreignKey.PrincipalKey, out var principal))
                {
                    continue;
                }

                if (ReferenceEquals(principal, table))
                {
                    if (IsNullable(table, foreignKey))
                    {
                        Defer(deferred, table, foreignKey);
                    }

                    continue;
                }

                edges.Add((table, principal, foreignKey));
            }
        }

        var position = new Dictionary<BackupTableManifest, int>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < tables.Count; i++)
        {
            position.TryAdd(tables[i], i);
        }

        var remaining = byKey.Values.ToList();
        var order = new List<BackupTableManifest>();
        var activeEdges = edges.ToList();
        while (remaining.Count > 0)
        {
            var ready = remaining.Where(t => !activeEdges.Any(e => ReferenceEquals(e.Dependent, t) && remaining.Contains(e.Principal))).ToList();
            if (ready.Count > 0)
            {
                foreach (var table in ready.OrderBy(t => position[t]))
                {
                    order.Add(table);
                    remaining.Remove(table);
                }

                continue;
            }

            // Cycle: break it by deferring a nullable foreign key between remaining tables.
            var breakable = activeEdges.FirstOrDefault(e => remaining.Contains(e.Dependent) && remaining.Contains(e.Principal) && IsNullable(e.Dependent, e.ForeignKey));
            if (breakable.Dependent is null)
            {
                throw new BackupException(
                    "The tables " + string.Join(", ", remaining.Select(t => t.Key)) + " reference each other with required foreign keys and cannot be restored in order.");
            }

            Defer(deferred, breakable.Dependent, breakable.ForeignKey);
            activeEdges.Remove(breakable);
        }

        return new BackupTableOrder(order, deferred.ToDictionary(p => p.Key, p => (IReadOnlySet<string>)p.Value, StringComparer.Ordinal));
    }

    private static bool IsNullable(BackupTableManifest table, BackupForeignKeyManifest foreignKey) =>
        foreignKey.Columns.All(name => table.Columns.FirstOrDefault(c => c.Name == name)?.IsNullable == true);

    private static void Defer(Dictionary<string, HashSet<string>> deferred, BackupTableManifest table, BackupForeignKeyManifest foreignKey)
    {
        if (!deferred.TryGetValue(table.Key, out var columns))
        {
            columns = new HashSet<string>(StringComparer.Ordinal);
            deferred[table.Key] = columns;
        }

        columns.UnionWith(foreignKey.Columns);
    }
}
