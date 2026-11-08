using Servora.Application.Common;

namespace Servora.Infrastructure.Itsm;

/// <summary>Seeds the incident categories, which the product needs even without sample tickets.</summary>
public sealed class ItsmReferenceSeeder : IDataSeeder
{
    /// <inheritdoc />
    public int Order => 490;

    /// <inheritdoc />
    public Task SeedAsync(IAppDbContext db, CancellationToken cancellationToken) =>
        ItsmDataSeeder.SeedCategoriesAsync(db, cancellationToken);
}
