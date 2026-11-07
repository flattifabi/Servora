using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Infrastructure.Backup;

namespace Servora.Infrastructure.Persistence;

/// <summary>
/// Creates or migrates the schema of the configured database and runs all <see cref="IDataSeeder"/>s.
/// Migrations are applied by <see cref="DatabaseMigrationRunner"/> (Backup module): a pre-migration backup is taken
/// first, a failed migration restores it automatically and stops startup (non-zero exit code), a legacy database
/// without migration history or a database newer than the application stops startup with an actionable message.
/// Builds without migrations (development before the first migration) create the schema with <c>EnsureCreated</c>.
/// </summary>
/// <param name="scopeFactory">The scope factory.</param>
/// <param name="logger">The logger.</param>
public sealed class DatabaseInitializer(IServiceScopeFactory scopeFactory, ILogger<DatabaseInitializer> logger)
{
    /// <summary>Initializes the database.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the schema is up to date and seed data exists.</returns>
    /// <exception cref="DatabaseMigrationException">Thrown when the schema cannot be brought up to date safely.</exception>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<ActorContext>().Set(ActorInfo.SystemNamed("System Setup"));

        var migrationRunner = scope.ServiceProvider.GetService<DatabaseMigrationRunner>();
        var factory = scope.ServiceProvider.GetRequiredService<AppDbContextFactory>();
        if (migrationRunner is not null)
        {
            try
            {
                await migrationRunner.MigrateAsync(cancellationToken);
            }
            catch (DatabaseMigrationException ex)
            {
                logger.LogCritical("Database initialization stopped: {Message}", ex.Message);
                throw;
            }
        }
        else
        {
            await using var schemaContext = factory.Create();
            await MigrateWithoutSafetyNetAsync(schemaContext, cancellationToken);
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
        if (db.Database.GetMigrations().Any())
        {
            var pendingMigrations = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pendingMigrations.Count > 0)
            {
                logger.LogInformation("Applying {Count} database migration(s): {Migrations}", pendingMigrations.Count, string.Join(", ", pendingMigrations));
                await db.Database.MigrateAsync(cancellationToken);
            }
        }
        else
        {
            var created = await db.Database.EnsureCreatedAsync(cancellationToken);
            logger.LogInformation(created ? "Database schema created." : "Database schema already exists.");
        }
    }
}
