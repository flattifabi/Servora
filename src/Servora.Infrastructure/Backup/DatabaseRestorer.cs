using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// Replaces all rows of the database with the rows of an archive inside one transaction: clear deferred foreign key
/// columns → delete all rows in reverse foreign key order → insert in foreign key order (deferred columns as null) →
/// update the deferred columns. Identifiers are delimited with <see cref="ISqlGenerationHelper"/>; every value is a parameter.
/// </summary>
internal static class DatabaseRestorer
{
    /// <summary>Restores the rows.</summary>
    /// <param name="db">The context (schema already at the backup's migration).</param>
    /// <param name="archive">The unlocked, verified archive.</param>
    /// <param name="targetSchema">The schema of the database at the backup's migration.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of restored rows.</returns>
    public static async Task<long> RestoreAsync(DbContext db, BackupArchiveReader archive, BackupSchemaSnapshot targetSchema, ILogger logger, CancellationToken cancellationToken)
    {
        var target = targetSchema.Tables.ToDictionary(t => t.Key, StringComparer.Ordinal);
        var source = archive.Manifest.Tables.ToDictionary(t => t.Key, StringComparer.Ordinal);
        Validate(source.Values, target, targetSchema.Migration);

        var order = BackupTableOrdering.Compute(targetSchema.Tables);
        var sql = db.GetService<ISqlGenerationHelper>();
        var dialect = RestoreDialect.For(db.Database.ProviderName);

        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = db.Database.GetDbConnection();
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            foreach (var table in order.InsertOrder)
            {
                var deferred = order.GetDeferred(table);
                if (deferred.Count > 0)
                {
                    var assignments = string.Join(", ", deferred.Select(c => $"{sql.DelimitIdentifier(c)} = NULL"));
                    var condition = string.Join(" OR ", deferred.Select(c => $"{sql.DelimitIdentifier(c)} IS NOT NULL"));
                    await ExecuteAsync(connection, transaction, $"UPDATE {Quote(sql, table)} SET {assignments} WHERE {condition}", cancellationToken);
                }
            }

            foreach (var table in order.InsertOrder.Reverse())
            {
                await ExecuteAsync(connection, transaction, $"DELETE FROM {Quote(sql, table)}", cancellationToken);
            }

            long total = 0;
            foreach (var targetTable in order.InsertOrder)
            {
                if (!source.TryGetValue(targetTable.Key, out var table) || table.RowCount == 0)
                {
                    continue;
                }

                var context = new RestoreTableContext(Quote(sql, table), Quote(sql, table), sql.DelimitIdentifier);
                await dialect.BeginInsertAsync(connection, transaction, context, cancellationToken);
                var inserted = await InsertRowsAsync(connection, transaction, sql, dialect, archive, table, order.GetDeferred(targetTable), context, cancellationToken);
                await dialect.EndInsertAsync(connection, transaction, context, cancellationToken);
                if (inserted != table.RowCount)
                {
                    throw new BackupIntegrityException($"Table '{table.Key}' contains {inserted} rows but the manifest declares {table.RowCount}.");
                }

                total += inserted;
                logger.LogDebug("Restored {Rows} row(s) into {Table}", inserted, table.Key);
            }

            foreach (var targetTable in order.InsertOrder)
            {
                var deferred = order.GetDeferred(targetTable);
                if (deferred.Count > 0 && source.TryGetValue(targetTable.Key, out var table) && table.RowCount > 0)
                {
                    await UpdateDeferredAsync(connection, transaction, sql, dialect, archive, table, deferred, cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return total;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static void Validate(IEnumerable<BackupTableManifest> source, Dictionary<string, BackupTableManifest> target, string? migration)
    {
        var schemaName = migration is null ? "the current database schema" : $"the database schema of migration '{migration}'";
        foreach (var table in source)
        {
            if (!target.TryGetValue(table.Key, out var targetTable))
            {
                throw new BackupException($"Table '{table.Key}' of the backup does not exist in {schemaName}.");
            }

            var missing = table.Columns.Select(c => c.Name).Except(targetTable.Columns.Select(c => c.Name), StringComparer.Ordinal).ToList();
            if (missing.Count > 0)
            {
                throw new BackupException($"Columns {string.Join(", ", missing)} of table '{table.Key}' do not exist in {schemaName}.");
            }
        }
    }

    private static async Task<long> InsertRowsAsync(
        DbConnection connection,
        DbTransaction transaction,
        ISqlGenerationHelper sql,
        RestoreDialect dialect,
        BackupArchiveReader archive,
        BackupTableManifest table,
        IReadOnlySet<string> deferred,
        RestoreTableContext context,
        CancellationToken cancellationToken)
    {
        var columnCount = table.Columns.Count;
        var deferredIndexes = table.Columns.Select((c, i) => (c, i)).Where(x => deferred.Contains(x.c.Name)).Select(x => x.i).ToArray();
        var rowsPerBatch = Math.Max(1, Math.Min(dialect.MaxRowsPerInsert, dialect.MaxParameters / columnCount));
        var columnList = string.Join(", ", table.Columns.Select(c => sql.DelimitIdentifier(c.Name)));
        var batch = new List<object?[]>(rowsPerBatch);
        long count = 0;

        using var reader = new TableDumpReader(archive.OpenEntry(archive.GetEntry(table.Entry)), columnCount);
        var values = new object?[columnCount];
        while (reader.ReadRow(values))
        {
            var row = (object?[])values.Clone();
            foreach (var index in deferredIndexes)
            {
                row[index] = null;
            }

            batch.Add(row);
            count++;
            if (batch.Count == rowsPerBatch)
            {
                await FlushAsync();
            }
        }

        if (batch.Count > 0)
        {
            await FlushAsync();
        }

        return count;

        async Task FlushAsync()
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandTimeout = 0;
            var rowsSql = new List<string>(batch.Count);
            var parameterIndex = 0;
            foreach (var row in batch)
            {
                var placeholders = new string[columnCount];
                for (var c = 0; c < columnCount; c++)
                {
                    var name = sql.GenerateParameterName("p" + parameterIndex++);
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = name;
                    dialect.SetValue(parameter, table.Columns[c], row[c]);
                    command.Parameters.Add(parameter);
                    placeholders[c] = dialect.Placeholder(name, table.Columns[c]);
                }

                rowsSql.Add("(" + string.Join(", ", placeholders) + ")");
            }

            command.CommandText = $"INSERT INTO {context.QuotedTable} ({columnList}){context.InsertModifier} VALUES {string.Join(", ", rowsSql)}";
            await command.ExecuteNonQueryAsync(cancellationToken);
            batch.Clear();
        }
    }

    private static async Task UpdateDeferredAsync(
        DbConnection connection,
        DbTransaction transaction,
        ISqlGenerationHelper sql,
        RestoreDialect dialect,
        BackupArchiveReader archive,
        BackupTableManifest table,
        IReadOnlySet<string> deferred,
        CancellationToken cancellationToken)
    {
        if (table.PrimaryKey.Count == 0)
        {
            throw new BackupException($"Table '{table.Key}' has self-referencing or circular foreign keys but no primary key, so it cannot be restored.");
        }

        var columns = table.Columns;
        var setIndexes = columns.Select((c, i) => (c, i)).Where(x => deferred.Contains(x.c.Name)).Select(x => x.i).ToArray();
        var keyIndexes = table.PrimaryKey.Select(name => columns.FindIndex(c => c.Name == name)).ToArray();
        if (setIndexes.Length == 0)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = 0;
        var assignments = new List<string>();
        var conditions = new List<string>();
        var parameters = new List<(DbParameter Parameter, int Column)>();
        foreach (var index in setIndexes.Concat(keyIndexes))
        {
            var name = sql.GenerateParameterName("p" + parameters.Count);
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            command.Parameters.Add(parameter);
            parameters.Add((parameter, index));
            var clause = $"{sql.DelimitIdentifier(columns[index].Name)} = {dialect.Placeholder(name, columns[index])}";
            if (parameters.Count <= setIndexes.Length)
            {
                assignments.Add(clause);
            }
            else
            {
                conditions.Add(clause);
            }
        }

        command.CommandText = $"UPDATE {Quote(sql, table)} SET {string.Join(", ", assignments)} WHERE {string.Join(" AND ", conditions)}";

        using var reader = new TableDumpReader(archive.OpenEntry(archive.GetEntry(table.Entry)), columns.Count);
        var values = new object?[columns.Count];
        while (reader.ReadRow(values))
        {
            if (setIndexes.All(i => values[i] is null))
            {
                continue;
            }

            foreach (var (parameter, column) in parameters)
            {
                dialect.SetValue(parameter, columns[column], values[column]);
            }

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static string Quote(ISqlGenerationHelper sql, BackupTableManifest table) => sql.DelimitIdentifier(table.Name, table.Schema);

    private static async Task ExecuteAsync(DbConnection connection, DbTransaction transaction, string commandText, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = 0;
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
