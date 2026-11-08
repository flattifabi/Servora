using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Catalog;
using Servora.Domain.Identity;

namespace Servora.Infrastructure.Persistence.Configurations.Catalog;

/// <summary>EF Core mapping of <see cref="CatalogFavorite"/>.</summary>
internal sealed class CatalogFavoriteConfiguration : IEntityTypeConfiguration<CatalogFavorite>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CatalogFavorite> builder)
    {
        builder.ToTable(CatalogTables.Favorite);
        builder.HasIndex(f => new { f.UserId, f.CatalogItemId }).IsUnique();
        builder.HasIndex(f => f.CatalogItemId);
        builder.HasOne<User>().WithMany().HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<CatalogItem>().WithMany().HasForeignKey(f => f.CatalogItemId).OnDelete(DeleteBehavior.Cascade);
    }
}
