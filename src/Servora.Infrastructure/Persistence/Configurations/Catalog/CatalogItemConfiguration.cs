using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Catalog;

namespace Servora.Infrastructure.Persistence.Configurations.Catalog;

/// <summary>EF Core mapping of <see cref="CatalogItem"/>.</summary>
internal sealed class CatalogItemConfiguration : IEntityTypeConfiguration<CatalogItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.ToTable(CatalogTables.Item);
        builder.Property(i => i.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(i => i.Name);
        builder.Property(i => i.ShortDescription).HasMaxLength(300);
        builder.Property(i => i.Description).HasMaxLength(8000);
        builder.Property(i => i.Icon).HasMaxLength(50).IsRequired();
        builder.Property(i => i.Price).HasPrecision(18, 2);
        builder.Property(i => i.FulfillmentTasks).HasMaxLength(2000);
        builder.HasIndex(i => i.CategoryId);
        builder.HasIndex(i => i.IsActive);
        builder.HasOne<CatalogCategory>().WithMany().HasForeignKey(i => i.CategoryId).OnDelete(DeleteBehavior.SetNull);
    }
}
