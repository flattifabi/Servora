using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// Reads table, column, primary key and foreign key metadata from the EF Core relational model. For a database with
/// migration history the model of the <em>applied</em> migration (<see cref="Migration.TargetModel"/>) is used, so a
/// pre-migration backup describes the schema that actually exists, not the newer model of the running version.
/// </summary>
public static class BackupSchemaReader
{
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";

    /// <summary>Reads the schema of the database's current migration.</summary>
    /// <param name="db">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The schema.</returns>
    /// <exception cref="BackupException">Thrown when the database is at a migration unknown to this version.</exception>
    public static async Task<BackupSchemaSnapshot> ReadCurrentAsync(DbContext db, CancellationToken cancellationToken)
    {
        var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        return ReadForMigration(db, applied.LastOrDefault());
    }

    /// <summary>Reads the schema as of a migration (or of the current model when <paramref name="migrationId"/> is null).</summary>
    /// <param name="db">The context.</param>
    /// <param name="migrationId">The migration identifier.</param>
    /// <returns>The schema.</returns>
    /// <exception cref="BackupException">Thrown when the migration is unknown to this version.</exception>
    public static BackupSchemaSnapshot ReadForMigration(DbContext db, string? migrationId)
    {
        var usesMigrations = db.Database.GetMigrations().Any();
        var model = migrationId is null ? db.GetService<IDesignTimeModel>().Model : GetMigrationModel(db, migrationId);
        var tables = ReadTables(model.GetRelationalModel(), db.Database.ProviderName);
        return new BackupSchemaSnapshot(migrationId, usesMigrations, BackupTableOrdering.Compute(tables).InsertOrder);
    }

    /// <summary>Converts a relational model to table manifests (in model order).</summary>
    /// <param name="relationalModel">The relational model.</param>
    /// <param name="providerName">The EF provider name.</param>
    /// <returns>The tables.</returns>
    public static List<BackupTableManifest> ReadTables(IRelationalModel relationalModel, string? providerName)
    {
        var result = new List<BackupTableManifest>();
        foreach (var table in relationalModel.Tables)
        {
            if (table.IsExcludedFromMigrations)
            {
                continue;
            }

            var manifest = new BackupTableManifest { Name = table.Name, Schema = table.Schema };
            foreach (var column in table.Columns)
            {
                if (column.ComputedColumnSql is not null || IsRowVersion(column, providerName))
                {
                    continue;
                }

                manifest.Columns.Add(new BackupColumnManifest
                {
                    Name = column.Name,
                    StoreType = column.StoreType,
                    IsNullable = column.IsNullable,
                    ClrType = column.ProviderClrType.Name,
                    DbType = column.StoreTypeMapping.DbType,
                });
            }

            if (table.PrimaryKey is { } primaryKey)
            {
                manifest.PrimaryKey.AddRange(primaryKey.Columns.Select(c => c.Name));
            }

            foreach (var foreignKey in table.ForeignKeyConstraints)
            {
                manifest.ForeignKeys.Add(new BackupForeignKeyManifest
                {
                    Name = foreignKey.Name,
                    Columns = foreignKey.Columns.Select(c => c.Name).ToList(),
                    PrincipalTable = foreignKey.PrincipalTable.Name,
                    PrincipalSchema = foreignKey.PrincipalTable.Schema,
                    PrincipalColumns = foreignKey.PrincipalColumns.Select(c => c.Name).ToList(),
                });
            }

            result.Add(manifest);
        }

        return result;
    }

    private static IModel GetMigrationModel(DbContext db, string migrationId)
    {
        var migrationsAssembly = db.GetService<IMigrationsAssembly>();
        if (!migrationsAssembly.Migrations.TryGetValue(migrationId, out var migrationType))
        {
            throw new BackupException(
                $"The database is at migration '{migrationId}', which this Servora version does not know. It was created or upgraded by a newer version.");
        }

        var migration = migrationsAssembly.CreateMigration(migrationType, db.Database.ProviderName!);
        if (migration.TargetModel is null)
        {
            // Hand-written migrations without a designer file: fall back to the current model.
            return db.GetService<IDesignTimeModel>().Model;
        }

        return db.GetService<IModelRuntimeInitializer>().Initialize(migration.TargetModel, designTime: true);
    }

    private static bool IsRowVersion(IColumn column, string? providerName) =>
        providerName == SqlServerProvider &&
        (string.Equals(column.StoreType, "rowversion", StringComparison.OrdinalIgnoreCase) || string.Equals(column.StoreType, "timestamp", StringComparison.OrdinalIgnoreCase));
}
