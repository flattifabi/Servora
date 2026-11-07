namespace Servora.Application.Common;

/// <summary>
/// Seeds reference / demo data for a module after the schema was created or migrated. Seeders must be idempotent:
/// they run on every start and must only insert data that is missing. Register as scoped services.
/// </summary>
public interface IDataSeeder
{
    /// <summary>Gets the execution order. Identity (roles, admin) uses 0; modules should use 100+.</summary>
    int Order { get; }

    /// <summary>Seeds the data.</summary>
    /// <param name="db">An open database context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when seeding is done.</returns>
    Task SeedAsync(IAppDbContext db, CancellationToken cancellationToken);
}
