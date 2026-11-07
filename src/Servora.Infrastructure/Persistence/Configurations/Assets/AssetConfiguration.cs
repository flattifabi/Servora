using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Assets;
using Servora.Domain.Cmdb;
using Servora.Domain.Common;

namespace Servora.Infrastructure.Persistence.Configurations.Assets;

/// <summary>EF Core mapping of <see cref="Asset"/>.</summary>
internal sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable(TableNames.Asset);
        builder.Property(a => a.Number).HasMaxLength(40).IsRequired();
        builder.HasIndex(a => a.Number).IsUnique();
        builder.Property(a => a.DisplayName).HasMaxLength(250).IsRequired();
        builder.HasIndex(a => a.DisplayName);
        builder.Property(a => a.Model).HasMaxLength(150);
        builder.Property(a => a.Manufacturer).HasMaxLength(128);
        builder.Property(a => a.Vendor).HasMaxLength(128);
        builder.Property(a => a.SerialNumber).HasMaxLength(128);
        builder.HasIndex(a => a.SerialNumber);
        builder.Property(a => a.AssetTag).HasMaxLength(64);
        builder.HasIndex(a => a.AssetTag);
        builder.Property(a => a.Location).HasMaxLength(128);
        builder.Property(a => a.Cost).HasPrecision(18, 2);
        builder.Property(a => a.Currency).HasMaxLength(3).IsRequired();
        builder.Property(a => a.Notes).HasMaxLength(4000);
        builder.Property(a => a.CustomFieldsJson).HasMaxLength(32_000);
        builder.HasIndex(a => new { a.State, a.Category });
        builder.HasIndex(a => a.AssignedToId);
        builder.HasIndex(a => a.ConfigurationItemId);
        builder.HasIndex(a => a.WarrantyEnd);
        builder.HasOne<ConfigurationItem>().WithMany().HasForeignKey(a => a.ConfigurationItemId).OnDelete(DeleteBehavior.SetNull);
    }
}
