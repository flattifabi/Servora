using Servora.Application.Common;

namespace Servora.Infrastructure.Cmdb;

/// <summary>Seeds the configuration item classes, which the product needs even without a sample service map.</summary>
public sealed class CmdbReferenceSeeder : IDataSeeder
{
    /// <inheritdoc />
    public int Order => 105;

    /// <inheritdoc />
    public Task SeedAsync(IAppDbContext db, CancellationToken cancellationToken) =>
        CmdbSeeder.SeedClassesAsync(db, cancellationToken);
}
