using System.Data;
using System.Data.Common;

namespace Servora.Infrastructure.Backup;

/// <summary>SQL Server specifics: snapshot isolation when enabled, typed parameters (<c>datetime2</c> precision) and <c>IDENTITY_INSERT</c>.</summary>
internal sealed class SqlServerRestoreDialect : RestoreDialect
{
    /// <inheritdoc />
    public override int MaxParameters => 2000;

    /// <inheritdoc />
    public override int MaxRowsPerInsert => 500;

    /// <inheritdoc />
    public override async Task<DbTransaction?> BeginBackupAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        // A consistent online backup needs snapshot isolation (ALTER DATABASE ... SET ALLOW_SNAPSHOT_ISOLATION ON);
        // without it the dump runs under READ COMMITTED (documented in docs/modules/backup.md).
        await using var command = CreateCommand(connection, null, "SELECT snapshot_isolation_state FROM sys.databases WHERE name = DB_NAME()");
        var state = await command.ExecuteScalarAsync(cancellationToken);
        var level = state is not null && Convert.ToInt32(state, System.Globalization.CultureInfo.InvariantCulture) == 1
            ? IsolationLevel.Snapshot
            : IsolationLevel.ReadCommitted;
        return await connection.BeginTransactionAsync(level, cancellationToken);
    }

    /// <inheritdoc />
    public override void SetValue(DbParameter parameter, BackupColumnManifest column, object? value)
    {
        // Without an explicit type SqlClient sends DateTime as legacy 'datetime' (3 ms precision) and untyped nulls as
        // nvarchar, which cannot be converted to varbinary. The column mapping's DbType avoids both.
        if (column.DbType is { } dbType)
        {
            parameter.DbType = dbType;
        }

        parameter.Value = value ?? DBNull.Value;
    }

    /// <inheritdoc />
    public override async Task BeginInsertAsync(DbConnection connection, DbTransaction transaction, RestoreTableContext context, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, "SELECT COUNT(*) FROM sys.identity_columns WHERE object_id = OBJECT_ID(@name)");
        AddParameter(command, "@name", context.QualifiedName);
        var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
        if (count > 0)
        {
            await using var enable = CreateCommand(connection, transaction, $"SET IDENTITY_INSERT {context.QuotedTable} ON");
            await enable.ExecuteNonQueryAsync(cancellationToken);
            context.IdentityInsertEnabled = true;
        }
    }

    /// <inheritdoc />
    public override async Task EndInsertAsync(DbConnection connection, DbTransaction transaction, RestoreTableContext context, CancellationToken cancellationToken)
    {
        if (context.IdentityInsertEnabled)
        {
            // Explicit inserts raise the identity seed automatically when needed, so no reseed is necessary.
            await using var disable = CreateCommand(connection, transaction, $"SET IDENTITY_INSERT {context.QuotedTable} OFF");
            await disable.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
