using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Servora.Infrastructure.Persistence;
using Servora.Persistence.PostgreSql;
using Servora.Persistence.Sqlite;
using Servora.Persistence.SqlServer;

namespace Servora.Tests.Unit.Persistence.Migrations;

/// <summary>
/// Builds <see cref="ServoraDbContext"/> instances for each shipped database provider exactly like production
/// (<see cref="DbContextOptionsProvider"/>): the provider's <see cref="IDatabaseProvider.Configure"/> plus the
/// provider-aware model cache key. The dummy connection strings are never opened.
/// </summary>
internal static class MigrationTestProviders
{
    /// <summary>The name suffix of the first migration of every provider.</summary>
    public const string InitialMigrationSuffix = "_InitialCreate";

    /// <summary>Gets the provider keys as theory data.</summary>
    public static TheoryData<string> Keys => new() { "Sqlite", "SqlServer", "PostgreSql" };

    /// <summary>Gets the provider for a key.</summary>
    /// <param name="key">The provider key.</param>
    /// <returns>The provider.</returns>
    public static IDatabaseProvider Get(string key) => key switch
    {
        "Sqlite" => new SqliteDatabaseProvider(),
        "SqlServer" => new SqlServerDatabaseProvider(),
        "PostgreSql" => new PostgreSqlDatabaseProvider(),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown provider."),
    };

    /// <summary>Gets a connection string for a key that is valid syntactically but never used to connect.</summary>
    /// <param name="key">The provider key.</param>
    /// <returns>The connection string.</returns>
    public static string DummyConnectionString(string key) => key switch
    {
        "Sqlite" => "Data Source=:memory:",
        "SqlServer" => "Server=localhost;Database=ServoraMigrationTests;Trusted_Connection=True;TrustServerCertificate=True",
        "PostgreSql" => "Host=localhost;Database=servora_migration_tests;Username=servora;Password=unused",
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown provider."),
    };

    /// <summary>Creates context options like <see cref="DbContextOptionsProvider.Build"/>.</summary>
    /// <param name="key">The provider key.</param>
    /// <param name="connectionString">The connection string (dummy when omitted).</param>
    /// <returns>The options.</returns>
    public static DbContextOptions<ServoraDbContext> CreateOptions(string key, string? connectionString = null)
    {
        var builder = new DbContextOptionsBuilder<ServoraDbContext>();
        Get(key).Configure(builder, connectionString ?? DummyConnectionString(key));
        builder.ReplaceService<IModelCacheKeyFactory, ProviderAwareModelCacheKeyFactory>();
        return builder.Options;
    }

    /// <summary>Creates a context for a provider with a dummy connection string.</summary>
    /// <param name="key">The provider key.</param>
    /// <returns>The context.</returns>
    public static ServoraDbContext CreateContext(string key) => new(CreateOptions(key));
}
