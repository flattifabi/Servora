using System.Data;
using System.Data.Common;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// PostgreSQL specifics: repeatable-read snapshot for backups, explicit <c>CAST</c> of every parameter to the column's
/// store type (PostgreSQL has no implicit text→uuid/jsonb conversions), <c>OVERRIDING SYSTEM VALUE</c> and sequence
/// reset for identity / serial columns.
/// </summary>
internal sealed class PostgreSqlRestoreDialect : RestoreDialect
{
    /// <inheritdoc />
    public override int MaxParameters => 30000;

    /// <inheritdoc />
    public override int MaxRowsPerInsert => 500;

    /// <inheritdoc />
    public override async Task<DbTransaction?> BeginBackupAsync(DbConnection connection, CancellationToken cancellationToken) =>
        await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);

    /// <inheritdoc />
    public override string Placeholder(string parameterName, BackupColumnManifest column)
    {
        // The store type was validated against a strict pattern when the manifest was read (BackupManifestValidator).
        if (!BackupManifestValidator.IsSafeStoreType(column.StoreType))
        {
            throw new BackupIntegrityException($"The store type of column '{column.Name}' is invalid.");
        }

        return $"CAST({parameterName} AS {column.StoreType})";
    }

    /// <inheritdoc />
    public override async Task BeginInsertAsync(DbConnection connection, DbTransaction transaction, RestoreTableContext context, CancellationToken cancellationToken)
    {
        const string sql =
            "SELECT a.attname, a.attidentity::text FROM pg_attribute a " +
            "WHERE a.attrelid = to_regclass(@name) AND a.attnum > 0 AND NOT a.attisdropped " +
            "AND (a.attidentity IN ('a', 'd') OR pg_get_serial_sequence(@name, a.attname) IS NOT NULL)";
        await using var command = CreateCommand(connection, transaction, sql);
        AddParameter(command, "@name", context.QualifiedName);
        var hasIdentity = false;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                context.GeneratedColumns.Add(reader.GetString(0));
                hasIdentity |= reader.GetString(1) is "a" or "d";
            }
        }

        if (hasIdentity)
        {
            context.InsertModifier = " OVERRIDING SYSTEM VALUE";
        }
    }

    /// <inheritdoc />
    public override async Task EndInsertAsync(DbConnection connection, DbTransaction transaction, RestoreTableContext context, CancellationToken cancellationToken)
    {
        foreach (var column in context.GeneratedColumns)
        {
            var quotedColumn = context.Delimit(column);
            var sql = $"SELECT setval(pg_get_serial_sequence(@name, @column), COALESCE((SELECT MAX({quotedColumn}) FROM {context.QuotedTable}), 0) + 1, false)";
            await using var command = CreateCommand(connection, transaction, sql);
            AddParameter(command, "@name", context.QualifiedName);
            AddParameter(command, "@column", column);
            await command.ExecuteScalarAsync(cancellationToken);
        }
    }
}
