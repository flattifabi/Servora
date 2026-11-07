using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Servora.Domain.Backup;
using Servora.Domain.Observability;
using Servora.Infrastructure.Backup;
using Servora.Tests.Unit.Backup.Infrastructure;
using Servora.Tests.Unit.Backup.Migrations;
using Servora.Tests.Unit.Backup.Model;

namespace Servora.Tests.Unit.Backup;

/// <summary>
/// Acceptance: restore migrates the database down to the backup's migration, replaces the rows and normal startup
/// migrates up again; automatic pre-migration backup and automatic restore after a failed migration.
/// </summary>
public sealed class MigrationRestoreTests
{
    private static readonly Guid s_parent = Guid.Parse("0195f000-0000-7000-8000-000000000001");
    private static readonly Guid s_child = Guid.Parse("0195f000-0000-7000-8000-000000000002");

    [Fact]
    public async Task Backup_DescribesTheSchemaOfTheAppliedMigration_NotTheNewerModel()
    {
        using var host = new BackupTestHost(NoteLabContext.Create);
        await CreateAtInitialAsync(host);

        var backup = await host.CreateBackupAsync();

        Assert.Equal(NoteMigrationSteps.Initial, backup.Manifest!.Migration);
        var notes = Assert.Single(backup.Manifest.Tables);
        Assert.DoesNotContain(notes.Columns, c => c.Name == "Tag");
        Assert.Equal(2, notes.RowCount);
    }

    [Fact]
    public async Task Restore_MigratesDownToTheBackupMigration_ThenStartupMigratesUp()
    {
        using var host = new BackupTestHost(NoteLabContext.Create);
        await CreateAtInitialAsync(host);
        var atInitial = await SnapshotAsync(host);
        var backup = await host.CreateBackupAsync();

        // Upgrade to the newest version and change data.
        await using (var db = host.CreateContext())
        {
            await db.Database.MigrateAsync();
            await db.Database.ExecuteSqlRawAsync("UPDATE \"Notes\" SET \"Tag\" = 'new', \"Title\" = 'Changed'");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Notes\" WHERE \"ParentId\" IS NOT NULL");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO \"Notes\" (\"Id\", \"Title\", \"ParentId\", \"Tag\") VALUES ('0195f000-0000-7000-8000-0000000000ff', 'Extra', NULL, 'x')");
        }

        var result = await host.Engine.RestoreAsync(backup.Path, new BackupRestoreOptions(), CancellationToken.None);

        Assert.Equal(NoteMigrationSteps.Initial, result.Migration);
        await using (var db = host.CreateContext())
        {
            Assert.Equal([NoteMigrationSteps.Initial], await db.Database.GetAppliedMigrationsAsync());
        }

        DatabaseSnapshot.AssertEqual(atInitial, await SnapshotAsync(host));

        // Normal startup: the migration runner takes a pre-migration backup and migrates to the latest version.
        await host.Migrations.MigrateAsync();
        await using (var db = (NoteLabContext)host.CreateContext())
        {
            Assert.Equal([NoteMigrationSteps.Initial, NoteMigrationSteps.AddTag], await db.Database.GetAppliedMigrationsAsync());
            var notes = await db.Set<LabNote>().OrderBy(n => n.Title).ToListAsync();
            Assert.Equal(["Child", "Parent"], notes.Select(n => n.Title));
            Assert.All(notes, n => Assert.Null(n.Tag));
            Assert.Equal(s_parent, notes.Single(n => n.Title == "Child").ParentId);
        }
    }

    [Fact]
    public async Task MigrationRunner_CreatesPreMigrationBackup_BeforeApplyingPendingMigrations()
    {
        using var host = new BackupTestHost(NoteLabContext.Create);
        await CreateAtInitialAsync(host);

        await host.Migrations.MigrateAsync();

        var safety = Assert.Single(host.Catalog.List(), e => e.Kind == BackupKind.PreMigration);
        Assert.Equal(NoteMigrationSteps.Initial, safety.Manifest!.Migration);
        Assert.Contains(host.Settings.GetState().PendingAudits, a => a.Action == "backup.created" && a.BackupId == safety.Id);
        await using var db = host.CreateContext();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task MigrationRunner_SkipsBackup_WhenDisabled_AndForFreshDatabases()
    {
        using var host = new BackupTestHost(NoteLabContext.Create);
        await host.Migrations.MigrateAsync();
        Assert.Empty(host.Catalog.List());

        using var second = new BackupTestHost(NoteLabContext.Create);
        await CreateAtInitialAsync(second);
        var schedule = second.Settings.GetSchedule();
        schedule.PreMigrationBackupEnabled = false;
        second.Settings.SaveSchedule(schedule);
        await second.Migrations.MigrateAsync();
        Assert.Empty(second.Catalog.List());
    }

    [Fact]
    public async Task FailedMigration_RestoresThePreMigrationBackup_AndStopsStartup()
    {
        using var host = new BackupTestHost(FailingNoteLabContext.Create);
        await CreateAtInitialAsync(host);
        var atInitial = await SnapshotAsync(host);

        var exception = await Assert.ThrowsAsync<DatabaseMigrationException>(() => host.Migrations.MigrateAsync());

        Assert.Contains("restored automatically", exception.Message);
        await using (var db = host.CreateContext())
        {
            // AddTag succeeded before Broken failed; the restore migrated down again to the backup's migration.
            Assert.Equal([NoteMigrationSteps.Initial], await db.Database.GetAppliedMigrationsAsync());
        }

        DatabaseSnapshot.AssertEqual(atInitial, await SnapshotAsync(host));
        var state = host.Settings.GetState();
        Assert.True(state.LastRestore?.Succeeded);
        Assert.Contains(state.PendingAudits, a => a.Action == "backup.restored" && a.Outcome == AuditOutcome.Success);
    }

    [Fact]
    public async Task LegacyEnsureCreatedDatabase_IsDetected_WithActionableMessage()
    {
        using var host = new BackupTestHost(NoteLabContext.Create);
        await using (var db = host.CreateContext())
        {
            await db.Database.OpenConnectionAsync();
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE \"Notes\" (\"Id\" TEXT NOT NULL PRIMARY KEY, \"Title\" TEXT NOT NULL, \"ParentId\" TEXT NULL, \"Tag\" TEXT NULL)");
        }

        var exception = await Assert.ThrowsAsync<DatabaseMigrationException>(() => host.Migrations.MigrateAsync());

        Assert.Contains("no migration history", exception.Message);
        Assert.Contains(DatabaseMigrationRunner.LegacyDatabaseHelp, exception.Message);
    }

    [Fact]
    public async Task DatabaseNewerThanTheApplication_StopsStartup()
    {
        using var host = new BackupTestHost(NoteLabContext.Create);
        await using (var db = host.CreateContext())
        {
            await db.Database.MigrateAsync();
            await db.Database.ExecuteSqlRawAsync("INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('20990101000000_Future', '10.0.10')");
        }

        var exception = await Assert.ThrowsAsync<DatabaseMigrationException>(() => host.Migrations.MigrateAsync());
        Assert.Contains("20990101000000_Future", exception.Message);

        // A backup of such a database is refused too: its schema is unknown to this version.
        await Assert.ThrowsAsync<BackupException>(() => host.CreateBackupAsync());
    }

    [Fact]
    public async Task Restore_IsRefused_ForUnknownMigration_OtherProvider_OrBackupWithoutMigrations()
    {
        using var host = new BackupTestHost(NoteLabContext.Create);
        await CreateAtInitialAsync(host);
        var backup = await host.CreateBackupAsync();
        var manifest = backup.Manifest!;

        manifest.Migration = "20990101000000_Future";
        Assert.Contains(host.Engine.CheckCompatibility(manifest), p => p.Contains("does not know", StringComparison.Ordinal));

        manifest.Migration = NoteMigrationSteps.Initial;
        manifest.ProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";
        Assert.Contains(host.Engine.CheckCompatibility(manifest), p => p.Contains("same provider", StringComparison.Ordinal));

        using var lab = new BackupTestHost(LabContext.Create);
        await SqliteRoundTripTests.SeedLabAsync(lab);
        var labBackup = await lab.CreateBackupAsync();
        Assert.Contains(host.Engine.CheckCompatibility(labBackup.Manifest!), p => p.Contains("without database migrations", StringComparison.Ordinal));
        await Assert.ThrowsAsync<BackupException>(() => host.Engine.RestoreAsync(labBackup.Path, new BackupRestoreOptions(), CancellationToken.None));
    }

    private static async Task CreateAtInitialAsync(BackupTestHost host)
    {
        await using var db = host.CreateContext();
        await db.GetService<IMigrator>().MigrateAsync(NoteMigrationSteps.Initial);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"Notes\" (\"Id\", \"Title\", \"ParentId\") VALUES ({0}, 'Parent', NULL), ({1}, 'Child', {0})",
            s_parent.ToString().ToUpperInvariant(),
            s_child.ToString().ToUpperInvariant());
    }

    private static async Task<SortedDictionary<string, List<string>>> SnapshotAsync(BackupTestHost host)
    {
        await using var db = host.CreateContext();
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        var rows = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT \"Id\", \"Title\", \"ParentId\" FROM \"Notes\"";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add($"{reader.GetValue(0)}|{reader.GetValue(1)}|{reader.GetValue(2)}");
            }
        }

        rows.Sort(StringComparer.Ordinal);
        return new SortedDictionary<string, List<string>> { ["Notes"] = rows };
    }
}
