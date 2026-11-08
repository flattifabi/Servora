using Microsoft.EntityFrameworkCore;
using Servora.Domain.Identity;
using Servora.Infrastructure.Backup;
using Servora.Infrastructure.Identity;

namespace Servora.Tests.Unit.Persistence.Migrations;

/// <summary>
/// Startup path (<c>DatabaseInitializer</c> → <c>DatabaseMigrationRunner</c> → seeders) on SQLite with the real
/// migrations and module registrations: a fresh database is created by migrations (never <c>EnsureCreated</c>) and
/// seeded, a second start is a no-op, and a legacy <c>EnsureCreated</c> database stops startup with instructions.
/// </summary>
public sealed class StartupMigrationTests
{
    [Fact]
    public async Task FreshDatabase_IsCreatedByMigrations_AndSeeded_WithoutPreMigrationBackup()
    {
        using var host = new StartupTestHost();

        await host.InitializeAsync();

        await using var db = host.CreateContext();
        Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync());
        Assert.True(await db.Set<User>().AnyAsync(u => u.UserName == IdentitySeeder.AdminUserName));
        Assert.Empty(host.BackupArchives());
    }

    [Fact]
    public async Task SecondStart_IsANoOp()
    {
        using var host = new StartupTestHost();
        await host.InitializeAsync();
        int users;
        await using (var db = host.CreateContext())
        {
            users = await db.Set<User>().CountAsync();
        }

        await host.InitializeAsync();

        await using (var db = host.CreateContext())
        {
            Assert.Equal(users, await db.Set<User>().CountAsync());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }

        Assert.Empty(host.BackupArchives());
    }

    [Fact]
    public async Task LegacyEnsureCreatedDatabase_StopsStartup_WithInstructions()
    {
        using var host = new StartupTestHost();
        await using (var db = host.CreateContext())
        {
            await db.Database.EnsureCreatedAsync();
        }

        var error = await Assert.ThrowsAsync<DatabaseMigrationException>(host.InitializeAsync);

        Assert.Contains("no migration history", error.Message, StringComparison.Ordinal);
        Assert.Contains(DatabaseMigrationRunner.LegacyDatabaseHelp, error.Message, StringComparison.Ordinal);
        await using (var db = host.CreateContext())
        {
            Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
            Assert.False(await db.Set<User>().AnyAsync());
        }
    }

    [Fact]
    public async Task WithoutBackupModule_MigrationsAreStillApplied()
    {
        using var host = new StartupTestHost(withBackupModule: false);

        await host.InitializeAsync();

        await using var db = host.CreateContext();
        Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync());
        Assert.True(await db.Set<User>().AnyAsync(u => u.UserName == IdentitySeeder.AdminUserName));
    }
}
