using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Catalog;

namespace Servora.Infrastructure.Persistence.Configurations.Catalog;

/// <summary>EF Core mapping of <see cref="CatalogCategory"/>.</summary>
internal sealed class CatalogCategoryConfiguration : IEntityTypeConfiguration<CatalogCategory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CatalogCategory> builder)
    {
        builder.ToTable(CatalogTables.Category);
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(c => c.Name).IsUnique();
        builder.Property(c => c.Description).HasMaxLength(1000);
        builder.Property(c => c.Icon).HasMaxLength(50).IsRequired();
    }
}
