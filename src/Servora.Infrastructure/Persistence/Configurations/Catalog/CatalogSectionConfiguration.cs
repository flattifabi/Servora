using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Catalog;

namespace Servora.Infrastructure.Persistence.Configurations.Catalog;

/// <summary>EF Core mapping of <see cref="CatalogSection"/>.</summary>
internal sealed class CatalogSectionConfiguration : IEntityTypeConfiguration<CatalogSection>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CatalogSection> builder)
    {
        builder.ToTable(CatalogTables.Section);
        builder.Property(s => s.Title).HasMaxLength(120).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(300);
        builder.HasIndex(s => new { s.CatalogItemId, s.Order });
        builder.HasOne<CatalogItem>().WithMany().HasForeignKey(s => s.CatalogItemId).OnDelete(DeleteBehavior.Cascade);
    }
}
