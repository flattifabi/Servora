using System.Collections.Concurrent;
using Servora.Domain.Platform;

namespace Servora.Application.Tables;

/// <summary>Creates and caches the <see cref="ITableAccessor"/> of a table.</summary>
internal static class TableAccessors
{
    private static readonly ConcurrentDictionary<Type, ITableAccessor> s_accessors = new();

    /// <summary>Gets the accessor of a table.</summary>
    /// <param name="table">The table.</param>
    /// <returns>The accessor.</returns>
    public static ITableAccessor For(TableDefinition table)
    {
        var type = table.IsCustom ? typeof(CustomRecord) : table.EntityType ?? throw new InvalidOperationException($"Table {table.Name} has no entity type.");
        return s_accessors.GetOrAdd(type, t => (ITableAccessor)Activator.CreateInstance(typeof(TableAccessor<>).MakeGenericType(t))!);
    }
}
