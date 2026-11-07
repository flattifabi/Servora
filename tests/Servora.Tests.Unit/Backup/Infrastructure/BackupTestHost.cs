using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Servora.Infrastructure.Backup;

namespace Servora.Tests.Unit.Backup.Infrastructure;

/// <summary>
/// Wires the real backup engine, catalog, settings, job runner, restore processor and migration runner against a
/// temporary data directory and a test database (temporary SQLite file).
/// </summary>
internal sealed class BackupTestHost : IDisposable
{
    /// <summary>The PBKDF2 iteration count used by tests (fast).</summary>
    public const int TestIterations = 10_000;

    /// <summary>Initializes a new instance of the <see cref="BackupTestHost"/> class.</summary>
    /// <param name="createContext">Creates a context for a connection string.</param>
    /// <param name="databaseFile">The SQLite file name inside the data directory.</param>
    public BackupTestHost(Func<string, DbContext> createContext, string databaseFile = "test.db")
    {
        Paths = new TestAppPaths();
        ConnectionString = $"Data Source={Path.Combine(Paths.DataDirectory, databaseFile)};Pooling=False";
        Database = new TestBackupDatabase(() => createContext(ConnectionString));
        Time = new ManualTimeProvider(new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero));
        Settings = new BackupSettingsStore(Paths, Time);
        Catalog = new BackupCatalog(Settings, NullLogger<BackupCatalog>.Instance);
        Lock = new BackupOperationLock(Settings);
        Engine = new BackupEngine(Database, Settings, Catalog, Time, NullLogger<BackupEngine>.Instance);
        Retention = new BackupRetentionService(Catalog, Time, NullLogger<BackupRetentionService>.Instance);
        Jobs = new BackupJobRunner(Engine, Settings, Lock, Retention, Passwords, Time, NullLogger<BackupJobRunner>.Instance) { Iterations = TestIterations };
        Requests = new RestoreRequestStore(Settings, DataProtection);
        Processor = new PendingRestoreProcessor(Requests, Engine, Settings, Lock, Time, NullLogger<PendingRestoreProcessor>.Instance);
        Migrations = new DatabaseMigrationRunner(Database, Jobs, Engine, Settings, Lock, Time, NullLogger<DatabaseMigrationRunner>.Instance);
    }

    /// <summary>Gets the temporary data directory.</summary>
    public TestAppPaths Paths { get; }

    /// <summary>Gets the SQLite connection string.</summary>
    public string ConnectionString { get; }

    /// <summary>Gets the database access.</summary>
    public TestBackupDatabase Database { get; }

    /// <summary>Gets the clock.</summary>
    public ManualTimeProvider Time { get; }

    /// <summary>Gets the data protection provider.</summary>
    public IDataProtectionProvider DataProtection { get; } = new EphemeralDataProtectionProvider();

    /// <summary>Gets the password source.</summary>
    public StaticPasswordSource Passwords { get; } = new();

    /// <summary>Gets the settings store.</summary>
    public BackupSettingsStore Settings { get; }

    /// <summary>Gets the catalog.</summary>
    public BackupCatalog Catalog { get; }

    /// <summary>Gets the operation lock.</summary>
    public BackupOperationLock Lock { get; }

    /// <summary>Gets the engine.</summary>
    public BackupEngine Engine { get; }

    /// <summary>Gets the retention service.</summary>
    public BackupRetentionService Retention { get; }

    /// <summary>Gets the job runner.</summary>
    public BackupJobRunner Jobs { get; }

    /// <summary>Gets the restore request store.</summary>
    public RestoreRequestStore Requests { get; }

    /// <summary>Gets the startup restore processor.</summary>
    public PendingRestoreProcessor Processor { get; }

    /// <summary>Gets the migration runner.</summary>
    public DatabaseMigrationRunner Migrations { get; }

    /// <summary>Creates a context.</summary>
    /// <returns>The context.</returns>
    public DbContext CreateContext() => Database.CreateContext();

    /// <summary>Creates an archive with the engine directly.</summary>
    /// <param name="password">The optional password.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The catalog entry.</returns>
    public Task<BackupCatalogEntry> CreateBackupAsync(string? password = null, Domain.Backup.BackupKind kind = Domain.Backup.BackupKind.Manual) =>
        Engine.CreateAsync(
            new BackupCreateOptions(kind, "tests", Settings.DefaultDestination) { Password = password, Iterations = TestIterations },
            CancellationToken.None);

    /// <inheritdoc />
    public void Dispose() => Paths.Dispose();
}
