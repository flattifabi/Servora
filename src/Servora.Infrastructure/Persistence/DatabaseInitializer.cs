using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Infrastructure.Backup;

namespace Servora.Infrastructure.Persistence;

/// <summary>
/// Brings the schema of the configured database up to date with EF Core migrations and runs all
/// <see cref="IDataSeeder"/>s. Every environment uses migrations (the provider projects <c>Servora.Persistence.*</c>
/// ship them, see <c>docs/development/migrations.md</c>); the schema is never created with <c>EnsureCreated</c>.
/// </summary>
/// <remarks>
/// Migrations are applied by <see cref="DatabaseMigrationRunner"/> (Backup module): a fresh database gets all
/// migrations, pending migrations on an existing database are preceded by a pre-migration backup, a failed migration
/// restores that backup automatically and stops startup (non-zero exit code), and a legacy database created by a
/// pre-release build with <c>EnsureCreated</c> (tables but no <c>__EFMigrationsHistory</c>) or a database newer than the
/// application stops startup with an actionable message (see <c>docs/operations/upgrade.md</c>). Hosts without the
/// Backup module apply pending migrations directly, without that safety net.
/// </remarks>
/// <param name="scopeFactory">The scope factory.</param>
/// <param name="logger">The logger.</param>
public sealed class DatabaseInitializer(IServiceScopeFactory scopeFactory, ILogger<DatabaseInitializer> logger)
{
    /// <summary>Migrates the database to the latest migration and seeds it.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the schema is up to date and seed data exists.</returns>
    /// <exception cref="DatabaseMigrationException">Thrown when the schema cannot be brought up to date safely.</exception>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<ActorContext>().Set(ActorInfo.SystemNamed("System Setup"));

        var migrationRunner = scope.ServiceProvider.GetService<DatabaseMigrationRunner>();
        var factory = scope.ServiceProvider.GetRequiredService<AppDbContextFactory>();
        try
        {
            if (migrationRunner is not null)
            {
                await migrationRunner.MigrateAsync(cancellationToken);
            }
            else
            {
                await using var schemaContext = factory.Create();
                await MigrateWithoutSafetyNetAsync(schemaContext, cancellationToken);
            }
        }
        catch (DatabaseMigrationException ex)
        {
            logger.LogCritical("Database initialization stopped: {Message}", ex.Message);
            throw;
        }

        await using var db = factory.Create();
        foreach (var seeder in scope.ServiceProvider.GetServices<IDataSeeder>().OrderBy(s => s.Order))
        {
            logger.LogDebug("Running seeder {Seeder}", seeder.GetType().Name);
            await seeder.SeedAsync(db, cancellationToken);
        }
    }

    private async Task MigrateWithoutSafetyNetAsync(ServoraDbContext db, CancellationToken cancellationToken)
    {
        if (!db.Database.GetMigrations().Any())
        {
            throw new DatabaseMigrationException(
                $"This Servora build contains no database migrations for the provider {db.Database.ProviderName}, so the schema cannot be created. " +
                "Use an official build, or add the migrations as described in docs/development/migrations.md.");
        }

        var pendingMigrations = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pendingMigrations.Count > 0)
        {
            logger.LogInformation("Applying {Count} database migration(s): {Migrations}", pendingMigrations.Count, string.Join(", ", pendingMigrations));
            await db.Database.MigrateAsync(cancellationToken);
        }
    }
}
