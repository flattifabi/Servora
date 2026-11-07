using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;

namespace Servora.Infrastructure.Backup;

/// <summary>Streams the rows of a table into a <see cref="TableDumpWriter"/> (one row in memory at a time).</summary>
internal static class DatabaseDumper
{
    /// <summary>Dumps a table.</summary>
    /// <param name="connection">The open connection.</param>
    /// <param name="transaction">The read transaction (snapshot), or <see langword="null"/> when it is managed by SQL text.</param>
    /// <param name="sql">The provider's SQL generation helper (identifier quoting).</param>
    /// <param name="table">The table.</param>
    /// <param name="output">The plain output stream.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows written.</returns>
    public static async Task<long> DumpTableAsync(
        DbConnection connection,
        DbTransaction? transaction,
        ISqlGenerationHelper sql,
        BackupTableManifest table,
        Stream output,
        CancellationToken cancellationToken)
    {
        var columns = string.Join(", ", table.Columns.Select(c => sql.DelimitIdentifier(c.Name)));
        var orderBy = table.PrimaryKey.Count > 0 ? " ORDER BY " + string.Join(", ", table.PrimaryKey.Select(sql.DelimitIdentifier)) : string.Empty;

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = 0;
        command.CommandText = $"SELECT {columns} FROM {sql.DelimitIdentifier(table.Name, table.Schema)}{orderBy}";

        using var writer = new TableDumpWriter(output, table.Columns.Count);
        var values = new object?[table.Columns.Count];
        await using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                for (var i = 0; i < values.Length; i++)
                {
                    try
                    {
                        values[i] = reader.GetValue(i);
                    }
                    catch (InvalidCastException ex)
                    {
                        throw new BackupException($"Column '{table.Key}.{table.Columns[i].Name}' contains a value that cannot be read.", ex);
                    }
                }

                try
                {
                    writer.WriteRow(values);
                }
                catch (NotSupportedException ex)
                {
                    throw new BackupException($"Column '{table.Key}': {ex.Message}", ex);
                }
            }
        }

        writer.Complete();
        return writer.RowCount;
    }
}
