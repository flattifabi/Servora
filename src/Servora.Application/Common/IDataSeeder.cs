namespace Servora.Application.Common;

/// <summary>
/// Seeds reference / demo data for a module after the schema was created or migrated. Seeders must be idempotent:
/// reference seeders run on every start and must only insert data that is missing. Demo seeders run only when sample
/// data was requested (setup option or <c>Servora:SeedDemoData</c>). Register as scoped services.
/// </summary>
public interface IDataSeeder
{
    /// <summary>Gets the execution order. Identity (roles, admin) uses 0; modules should use 100+.</summary>
    int Order { get; }

    /// <summary>Gets a value indicating whether this seeder creates sample records rather than data the product needs.</summary>
    bool IsDemoData => false;

    /// <summary>Seeds the data.</summary>
    /// <param name="db">An open database context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when seeding is done.</returns>
    Task SeedAsync(IAppDbContext db, CancellationToken cancellationToken);
}
