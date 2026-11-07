using Microsoft.EntityFrameworkCore;
using Npgsql;
using Servora.Infrastructure.Persistence;

namespace Servora.Persistence.PostgreSql;

/// <summary>PostgreSQL implementation of <see cref="IDatabaseProvider"/>. PostgreSQL 13+.</summary>
public sealed class PostgreSqlDatabaseProvider : IDatabaseProvider
{
    /// <inheritdoc />
    public string Key => "PostgreSql";

    /// <inheritdoc />
    public string DisplayName => "PostgreSQL";

    /// <inheritdoc />
    public string ExampleConnectionString => "Host=localhost;Port=5432;Database=servora;Username=servora;Password=***;SSL Mode=Prefer";

    /// <inheritdoc />
    public void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseNpgsql(connectionString, o =>
        {
            o.MigrationsAssembly(typeof(PostgreSqlDatabaseProvider).Assembly.GetName().Name);
            o.EnableRetryOnFailure(3);
        });

    /// <inheritdoc />
    public async Task<string?> TestConnectionAsync(string connectionString, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
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
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
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
