using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Configuration;

namespace Servora.Tests.Unit.Persistence.Migrations;

/// <summary>The setup wizard creates the schema with migrations and refuses databases it cannot migrate.</summary>
public sealed class SetupSchemaTests
{
    /// <summary>Installing into an empty SQLite database applies all migrations.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Install_IntoEmptyDatabase_AppliesMigrations()
    {
        using var host = new StartupTestHost(configureDatabase: false);
        var setup = host.GetRequiredService<ISetupService>();

        var result = await setup.InstallAsync("Sqlite", host.ConnectionString, new SetupOrganizationInput { CompanyName = "Test Co" }, null);

        Assert.True(result.Succeeded, result.Error);
        await using var db = host.CreateContext();
        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains(applied, m => m.EndsWith("_InitialCreate", StringComparison.Ordinal));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    /// <summary>A database with tables but no migration history is refused instead of half-migrated.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Install_IntoDatabaseWithForeignTables_IsRefused()
    {
        using var host = new StartupTestHost(configureDatabase: false);
        await using (var connection = new SqliteConnection(host.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE customer (id INTEGER PRIMARY KEY, name TEXT)";
            await command.ExecuteNonQueryAsync();
        }

        var setup = host.GetRequiredService<ISetupService>();
        var result = await setup.InstallAsync("Sqlite", host.ConnectionString, new SetupOrganizationInput { CompanyName = "Test Co" }, null);

        Assert.False(result.Succeeded);
        Assert.Contains("no Servora migration history", result.Error, StringComparison.Ordinal);
    }
}
