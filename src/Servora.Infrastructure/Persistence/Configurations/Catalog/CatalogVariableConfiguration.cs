using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Catalog;

namespace Servora.Infrastructure.Persistence.Configurations.Catalog;

/// <summary>EF Core mapping of <see cref="CatalogVariable"/>.</summary>
internal sealed class CatalogVariableConfiguration : IEntityTypeConfiguration<CatalogVariable>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CatalogVariable> builder)
    {
        builder.ToTable(CatalogTables.Variable);
        builder.Property(v => v.Name).HasMaxLength(64).IsRequired();
        builder.Property(v => v.Label).HasMaxLength(200).IsRequired();
        builder.Property(v => v.HelpText).HasMaxLength(1000);
        builder.Property(v => v.Options).HasMaxLength(8000);
        builder.Property(v => v.DefaultValue).HasMaxLength(1000);
        builder.Property(v => v.MinValue).HasPrecision(18, 4);
        builder.Property(v => v.MaxValue).HasPrecision(18, 4);
        builder.HasIndex(v => new { v.CatalogItemId, v.Name }).IsUnique();
        builder.HasOne<CatalogItem>().WithMany().HasForeignKey(v => v.CatalogItemId).OnDelete(DeleteBehavior.Cascade);
    }
}
