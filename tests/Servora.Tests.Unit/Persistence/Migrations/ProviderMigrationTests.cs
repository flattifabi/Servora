using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace Servora.Tests.Unit.Persistence.Migrations;

/// <summary>
/// Checks the migrations of every provider project without a database server: the model snapshot matches the current
/// EF model (fails when somebody changed an entity or configuration without adding a migration), every migration keeps
/// its designer file (the backup module reads <see cref="Migration.TargetModel"/>) and the generated SQL script creates
/// every table of the model.
/// </summary>
public sealed class ProviderMigrationTests
{
    /// <summary>Gets the provider keys.</summary>
    public static TheoryData<string> Providers => MigrationTestProviders.Keys;

    [Theory]
    [MemberData(nameof(Providers))]
    public void Model_HasNoPendingChanges_ComparedToTheMigrationSnapshot(string provider)
    {
        using var db = MigrationTestProviders.CreateContext(provider);

        Assert.True(
            !db.Database.HasPendingModelChanges(),
            $"The EF model has changes that are not covered by a {provider} migration. Add a migration for every provider " +
            "as described in docs/development/migrations.md.");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void Migrations_LiveInTheProviderProject_StartWithInitialCreate_AndKeepTheirDesignerFiles(string provider)
    {
        using var db = MigrationTestProviders.CreateContext(provider);
        var migrationsAssembly = db.GetService<IMigrationsAssembly>();

        Assert.Equal(MigrationTestProviders.Get(provider).GetType().Assembly, migrationsAssembly.Assembly);
        Assert.NotNull(migrationsAssembly.ModelSnapshot);
        var ids = migrationsAssembly.Migrations.Keys.ToList();
        Assert.NotEmpty(ids);
        Assert.EndsWith(MigrationTestProviders.InitialMigrationSuffix, ids[0], StringComparison.Ordinal);
        foreach (var (id, type) in migrationsAssembly.Migrations)
        {
            var migration = migrationsAssembly.CreateMigration(type, db.Database.ProviderName!);
            Assert.True(migration.TargetModel is not null, $"Migration {id} has no designer file (TargetModel); the backup module needs it.");
        }
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void MigrationScript_CreatesEveryTableOfTheModel(string provider)
    {
        using var db = MigrationTestProviders.CreateContext(provider);
        var sql = db.GetService<ISqlGenerationHelper>();

        var script = db.GetService<IMigrator>().GenerateScript();

        var missing = db.GetService<IDesignTimeModel>().Model.GetRelationalModel().Tables
            .Where(t => !t.IsExcludedFromMigrations)
            .Select(t => sql.DelimitIdentifier(t.Name, t.Schema))
            .Where(name => !script.Contains("CREATE TABLE " + name + " ", StringComparison.Ordinal))
            .ToList();
        Assert.True(missing.Count == 0, $"The {provider} migration script does not create: {string.Join(", ", missing)}");
    }
}
