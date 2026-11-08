using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Servora.Domain.Identity;
using Servora.Infrastructure.Persistence;

namespace Servora.Tests.Unit.Persistence.Migrations;

/// <summary>
/// Applies all SQLite migrations to an empty in-memory database (shared open connection) and compares the resulting
/// schema with the EF model: every table and every column of the model must exist.
/// </summary>
public sealed class SqliteMigrationApplyTests : IDisposable
{
    private readonly string _connectionString = $"Data Source=migrations-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _connection;

    /// <summary>Initializes a new instance of the <see cref="SqliteMigrationApplyTests"/> class.</summary>
    public SqliteMigrationApplyTests()
    {
        // Keeps the shared in-memory database alive while the contexts open their own connections.
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
    }

    [Fact]
    public async Task Migrate_OnAnEmptyDatabase_AppliesInitialCreate_AndHasNoPendingMigrations()
    {
        await using var db = CreateContext();

        await db.Database.MigrateAsync();

        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        Assert.Contains(applied, id => id.EndsWith(MigrationTestProviders.InitialMigrationSuffix, StringComparison.Ordinal));
        Assert.Equal(db.Database.GetMigrations(), applied);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Migrate_CreatesEveryTableAndColumnOfTheModel()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var tables = await ReadTablesAsync();
        var problems = new List<string>();
        foreach (var table in db.GetService<IDesignTimeModel>().Model.GetRelationalModel().Tables.Where(t => !t.IsExcludedFromMigrations))
        {
            if (!tables.Contains(table.Name))
            {
                problems.Add($"table {table.Name} is missing");
                continue;
            }

            var columns = await ReadColumnsAsync(table.Name);
            problems.AddRange(table.Columns.Where(c => !columns.Contains(c.Name)).Select(c => $"column {table.Name}.{c.Name} is missing"));
        }

        Assert.True(problems.Count == 0, string.Join("; ", problems));
        Assert.Contains("__EFMigrationsHistory", tables);
    }

    [Fact]
    public async Task Migrate_IsIdempotent_AndTheSchemaAcceptsData()
    {
        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync();
        }

        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(await db.Set<User>().AnyAsync());
        }
    }

    /// <inheritdoc />
    public void Dispose() => _connection.Dispose();

    private ServoraDbContext CreateContext() => new(MigrationTestProviders.CreateOptions("Sqlite", _connectionString));

    private async Task<HashSet<string>> ReadTablesAsync()
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    private async Task<HashSet<string>> ReadColumnsAsync(string table)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT name FROM pragma_table_info($table)";
        command.Parameters.AddWithValue("$table", table);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }
}
