using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Catalog;
using Servora.Domain.Common;
using Servora.Infrastructure.Persistence.Configurations.Itsm;

namespace Servora.Infrastructure.Persistence.Configurations.Catalog;

/// <summary>EF Core mapping of <see cref="RequestItem"/>.</summary>
internal sealed class RequestItemConfiguration : IEntityTypeConfiguration<RequestItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RequestItem> builder)
    {
        builder.ConfigureTaskRecord(TableNames.RequestItem);
        builder.Property(r => r.UnitPrice).HasPrecision(18, 2);
        builder.Property(r => r.OneTimePrice).HasPrecision(18, 2);
        builder.Property(r => r.VariablesJson).HasMaxLength(32_000);
        builder.HasIndex(r => r.RequestId);
        builder.HasIndex(r => r.CatalogItemId);
        builder.HasIndex(r => r.RequestedForId);
        builder.HasIndex(r => r.State);
        builder.HasOne<Request>().WithMany().HasForeignKey(r => r.RequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<CatalogItem>().WithMany().HasForeignKey(r => r.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
