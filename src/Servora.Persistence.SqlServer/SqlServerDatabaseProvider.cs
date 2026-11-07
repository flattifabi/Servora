using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Servora.Infrastructure.Persistence;

namespace Servora.Persistence.SqlServer;

/// <summary>Microsoft SQL Server implementation of <see cref="IDatabaseProvider"/>. Microsoft SQL Server 2019+ / Azure SQL.</summary>
public sealed class SqlServerDatabaseProvider : IDatabaseProvider
{
    /// <inheritdoc />
    public string Key => "SqlServer";

    /// <inheritdoc />
    public string DisplayName => "Microsoft SQL Server";

    /// <inheritdoc />
    public string ExampleConnectionString => "Server=localhost;Database=Servora;Trusted_Connection=True;TrustServerCertificate=False;Encrypt=True";

    /// <inheritdoc />
    public void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseSqlServer(connectionString, o =>
        {
            o.MigrationsAssembly(typeof(SqlServerDatabaseProvider).Assembly.GetName().Name);
            o.EnableRetryOnFailure(3);
        });

    /// <inheritdoc />
    public async Task<string?> TestConnectionAsync(string connectionString, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(connectionString);
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
        var builder = new SqlConnectionStringBuilder(connectionString);
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
