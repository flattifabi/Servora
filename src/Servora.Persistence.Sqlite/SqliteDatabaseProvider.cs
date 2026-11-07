using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Servora.Infrastructure.Persistence;

namespace Servora.Persistence.Sqlite;

/// <summary>SQLite implementation of <see cref="IDatabaseProvider"/>. Lightweight file database. Ideal for evaluation and small installations (single node).</summary>
public sealed class SqliteDatabaseProvider : IDatabaseProvider
{
    /// <inheritdoc />
    public string Key => "Sqlite";

    /// <inheritdoc />
    public string DisplayName => "SQLite";

    /// <inheritdoc />
    public string ExampleConnectionString => "Data Source={DataDirectory}/servora.db";

    /// <inheritdoc />
    public void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseSqlite(connectionString, o => o.MigrationsAssembly(typeof(SqliteDatabaseProvider).Assembly.GetName().Name));

    /// <inheritdoc />
    public async Task<string?> TestConnectionAsync(string connectionString, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Return the provider message only; it never contains the password.
            return ex.Message;
        }
    }

    /// <inheritdoc />
    public string Redact(string connectionString)
    {
        try
        {
        // SQLite connection strings may contain an encryption password.
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (!string.IsNullOrEmpty(builder.Password))
        {
            builder.Password = "***";
        }

        return builder.ToString();
        }
        catch (ArgumentException)
        {
            return "(invalid connection string)";
        }
    }
}
