using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Domain.Platform;

namespace Servora.Application.Tables;

/// <summary>
/// Maintains the <see cref="RecordFieldValue"/> index for custom fields (custom table columns and custom fields of
/// typed tables). Empty values have no index row.
/// </summary>
public static class RecordFieldIndexer
{
    /// <summary>Synchronizes the index rows of a record with its current custom field values and saves the changes.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="table">The table definition.</param>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="values">The current field values (canonical or JSON-normalized).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the index is up to date.</returns>
    public static async Task SyncAsync(IAppDbContext db, TableDefinition table, Guid recordId, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken)
    {
        var existing = await db.Set<RecordFieldValue>()
            .Where(v => v.TableName == table.Name && v.RecordId == recordId)
            .Take(1000)
            .ToListAsync(cancellationToken);
        var byField = existing.ToDictionary(v => v.Field, StringComparer.OrdinalIgnoreCase);

        foreach (var field in table.Fields.Where(f => f.IsCustom))
        {
            values.TryGetValue(field.Name, out var value);
            var row = byField.GetValueOrDefault(field.Name) ?? new RecordFieldValue { TableName = table.Name, RecordId = recordId, Field = field.Name };
            if (FieldIndexNormalizer.Fill(field.Type, value, row))
            {
                if (!byField.ContainsKey(field.Name))
                {
                    db.Set<RecordFieldValue>().Add(row);
                }
            }
            else if (byField.ContainsKey(field.Name))
            {
                db.Set<RecordFieldValue>().Remove(row);
            }

            byField.Remove(field.Name);
        }

        // Rows of fields that no longer exist.
        foreach (var orphan in byField.Values)
        {
            db.Set<RecordFieldValue>().Remove(orphan);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Removes all index rows of a record.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="tableName">The technical table name.</param>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the rows are removed.</returns>
    public static Task RemoveAsync(IAppDbContext db, string tableName, Guid recordId, CancellationToken cancellationToken) =>
        db.Set<RecordFieldValue>().Where(v => v.TableName == tableName && v.RecordId == recordId).ExecuteDeleteAsync(cancellationToken);
}
