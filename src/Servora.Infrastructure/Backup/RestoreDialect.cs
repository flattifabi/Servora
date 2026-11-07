using System.Data;
using System.Data.Common;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// The few provider-specific details of the logical dump/restore: snapshot isolation for consistent online backups,
/// parameter typing and identity columns. Everything else is provider-neutral SQL with delimited identifiers and
/// parameters. This is the only place in Servora that issues provider-specific SQL (see ADR-015).
/// </summary>
internal class RestoreDialect
{
    /// <summary>The EF Core SQLite provider name.</summary>
    public const string SqliteProvider = "Microsoft.EntityFrameworkCore.Sqlite";

    /// <summary>The EF Core SQL Server provider name.</summary>
    public const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";

    /// <summary>The EF Core PostgreSQL provider name.</summary>
    public const string PostgreSqlProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";

    /// <summary>Gets the maximum number of parameters per command.</summary>
    public virtual int MaxParameters => 900;

    /// <summary>Gets the maximum number of rows per multi-row <c>INSERT</c>.</summary>
    public virtual int MaxRowsPerInsert => 100;

    /// <summary>Returns the dialect of an EF Core provider.</summary>
    /// <param name="providerName">The provider name.</param>
    /// <returns>The dialect.</returns>
    public static RestoreDialect For(string? providerName) => providerName switch
    {
        SqliteProvider => new SqliteRestoreDialect(),
        SqlServerProvider => new SqlServerRestoreDialect(),
        PostgreSqlProvider => new PostgreSqlRestoreDialect(),
        _ => new RestoreDialect(),
    };

    /// <summary>Starts the read transaction of a backup (a consistent snapshot where the provider offers one).</summary>
    /// <param name="connection">The open connection.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The transaction to pass to the dump commands, or <see langword="null"/> when the snapshot is managed by SQL text.</returns>
    public virtual async Task<DbTransaction?> BeginBackupAsync(DbConnection connection, CancellationToken cancellationToken) =>
        await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

    /// <summary>Ends the read transaction of a backup.</summary>
    /// <param name="connection">The open connection.</param>
    /// <param name="transaction">The transaction returned by <see cref="BeginBackupAsync"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the transaction ended.</returns>
    public virtual async Task EndBackupAsync(DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            await transaction.DisposeAsync();
        }
    }

    /// <summary>Returns the SQL placeholder of a parameter for a column.</summary>
    /// <param name="parameterName">The parameter name including prefix.</param>
    /// <param name="column">The column.</param>
    /// <returns>The placeholder.</returns>
    public virtual string Placeholder(string parameterName, BackupColumnManifest column) => parameterName;

    /// <summary>Assigns a value to a parameter.</summary>
    /// <param name="parameter">The parameter.</param>
    /// <param name="column">The column.</param>
    /// <param name="value">The value or <see langword="null"/>.</param>
    public virtual void SetValue(DbParameter parameter, BackupColumnManifest column, object? value) => parameter.Value = value ?? DBNull.Value;

    /// <summary>Prepares inserting explicit values into a table (identity columns).</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="context">The table context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when prepared.</returns>
    public virtual Task BeginInsertAsync(DbConnection connection, DbTransaction transaction, RestoreTableContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>Finishes inserting into a table (switches identity insert off, resets generators).</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="context">The table context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when finished.</returns>
    public virtual Task EndInsertAsync(DbConnection connection, DbTransaction transaction, RestoreTableContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>Creates a command bound to the connection and transaction.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="sql">The SQL text.</param>
    /// <returns>The command.</returns>
    protected static DbCommand CreateCommand(DbConnection connection, DbTransaction? transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.CommandTimeout = 0;
        return command;
    }

    /// <summary>Adds a parameter.</summary>
    /// <param name="command">The command.</param>
    /// <param name="name">The parameter name including prefix.</param>
    /// <param name="value">The value.</param>
    protected static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
