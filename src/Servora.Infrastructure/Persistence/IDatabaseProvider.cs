using Microsoft.EntityFrameworkCore;

namespace Servora.Infrastructure.Persistence;

/// <summary>
/// A supported database engine. Implementations live in the <c>Servora.Persistence.*</c> projects together with the
/// provider-specific migrations, keeping provider packages out of the core layers.
/// </summary>
public interface IDatabaseProvider
{
    /// <summary>Gets the stable provider key stored in the database settings: <c>Sqlite</c>, <c>SqlServer</c> or <c>PostgreSql</c>.</summary>
    string Key { get; }

    /// <summary>Gets the display name.</summary>
    string DisplayName { get; }

    /// <summary>Gets an example connection string shown in the setup wizard.</summary>
    string ExampleConnectionString { get; }

    /// <summary>Configures EF Core for this provider, including the migrations assembly.</summary>
    /// <param name="builder">The options builder.</param>
    /// <param name="connectionString">The connection string.</param>
    void Configure(DbContextOptionsBuilder builder, string connectionString);

    /// <summary>Opens a connection to validate the connection string. Never throws; returns a user-safe error instead.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="null"/> on success, otherwise a user-safe error message without secrets.</returns>
    Task<string?> TestConnectionAsync(string connectionString, CancellationToken cancellationToken);

    /// <summary>Returns the connection string with passwords and tokens masked, for display and logging.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The redacted connection string.</returns>
    string Redact(string connectionString);
}
