using System.Data.Common;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// SQLite specifics: the backup reads inside a <c>BEGIN DEFERRED</c> transaction, which in WAL mode is a consistent
/// snapshot that does not block writers (an ADO.NET transaction would start with <c>BEGIN IMMEDIATE</c> and block them).
/// </summary>
internal sealed class SqliteRestoreDialect : RestoreDialect
{
    /// <inheritdoc />
    public override async Task<DbTransaction?> BeginBackupAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, null, "BEGIN DEFERRED");
        await command.ExecuteNonQueryAsync(cancellationToken);
        return null;
    }

    /// <inheritdoc />
    public override async Task EndBackupAsync(DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, null, "COMMIT");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
