using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;

namespace Servora.Infrastructure.Persistence;

/// <summary>
/// Creates or migrates the schema of the configured database and runs all <see cref="IDataSeeder"/>s.
/// If the provider's migrations assembly contains migrations they are applied; otherwise (development builds before
/// the first migration was generated) the schema is created with <c>EnsureCreated</c>.
/// </summary>
/// <param name="scopeFactory">The scope factory.</param>
/// <param name="logger">The logger.</param>
public sealed class DatabaseInitializer(IServiceScopeFactory scopeFactory, ILogger<DatabaseInitializer> logger)
{
    /// <summary>Initializes the database.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the schema is up to date and seed data exists.</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<ActorContext>().Set(ActorInfo.SystemNamed("System Setup"));
        var factory = scope.ServiceProvider.GetRequiredService<AppDbContextFactory>();
        await using var db = factory.Create();

        var migrations = db.Database.GetMigrations().ToList();
        if (migrations.Count > 0)
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

        foreach (var seeder in scope.ServiceProvider.GetServices<IDataSeeder>().OrderBy(s => s.Order))
        {
            logger.LogDebug("Running seeder {Seeder}", seeder.GetType().Name);
            await seeder.SeedAsync(db, cancellationToken);
        }
    }
}
