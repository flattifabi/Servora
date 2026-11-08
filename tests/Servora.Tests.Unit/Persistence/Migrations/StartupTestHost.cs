using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Servora.Application.Common;
using Servora.Infrastructure.Backup;
using Servora.Infrastructure.Modules;
using Servora.Infrastructure.Persistence;
using Servora.Persistence.Sqlite;
using Servora.Tests.Unit.Backup.Infrastructure;

namespace Servora.Tests.Unit.Persistence.Migrations;

/// <summary>
/// Wires the real infrastructure registrations (<c>AddServoraInfrastructure</c>, every module, seeders, the backup
/// module's <see cref="DatabaseMigrationRunner"/>) like <c>Program.cs</c>, against a temporary data directory and a
/// SQLite database file configured through <c>Servora:Database</c>.
/// </summary>
internal sealed class StartupTestHost : IDisposable
{
    private readonly ServiceProvider _services;

    /// <summary>Initializes a new instance of the <see cref="StartupTestHost"/> class.</summary>
    /// <param name="withBackupModule">Whether the backup module's migration runner stays registered.</param>
    /// <param name="configureDatabase">Whether the database is configured (otherwise the host is in setup mode).</param>
    public StartupTestHost(bool withBackupModule = true, bool configureDatabase = true)
    {
        ConnectionString = $"Data Source={Path.Combine(Paths.DataDirectory, "servora.db")};Pooling=False";
        var values = configureDatabase
            ? new Dictionary<string, string?>
            {
                ["Servora:Database:Provider"] = "Sqlite",
                ["Servora:Database:ConnectionString"] = ConnectionString,
            }
            : new Dictionary<string, string?>();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IAppPaths>(Paths);
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.TryAddSingleton(TimeProvider.System);
        services.AddServoraInfrastructure();
        services.AddSingleton<IDatabaseProvider, SqliteDatabaseProvider>();
        if (!withBackupModule)
        {
            services.RemoveAll<DatabaseMigrationRunner>();
        }

        _services = services.BuildServiceProvider();
    }

    /// <summary>Gets the temporary data directory.</summary>
    public TestAppPaths Paths { get; } = new();

    /// <summary>Gets the SQLite connection string.</summary>
    public string ConnectionString { get; }

    /// <summary>Runs the startup database initialization like <c>Program.cs</c>.</summary>
    /// <returns>A task that completes when the database is initialized.</returns>
    public Task InitializeAsync() => _services.GetRequiredService<DatabaseInitializer>().InitializeAsync();

    /// <summary>Gets a registered service.</summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <returns>The service.</returns>
    public T GetRequiredService<T>()
        where T : notnull => _services.GetRequiredService<T>();

    /// <summary>Creates a context for the configured database.</summary>
    /// <returns>The context.</returns>
    public ServoraDbContext CreateContext() => new(MigrationTestProviders.CreateOptions("Sqlite", ConnectionString));

    /// <summary>Lists the backup archives written to the data directory.</summary>
    /// <returns>The archive paths.</returns>
    public IReadOnlyList<string> BackupArchives() =>
        Directory.EnumerateFiles(Paths.DataDirectory, "*.svbak", SearchOption.AllDirectories).ToList();

    /// <inheritdoc />
    public void Dispose()
    {
        _services.Dispose();
        Paths.Dispose();
    }
}
