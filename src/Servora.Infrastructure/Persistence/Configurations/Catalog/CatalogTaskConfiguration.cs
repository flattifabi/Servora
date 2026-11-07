using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Catalog;
using Servora.Domain.Common;
using Servora.Infrastructure.Persistence.Configurations.Itsm;

namespace Servora.Infrastructure.Persistence.Configurations.Catalog;

/// <summary>EF Core mapping of <see cref="CatalogTask"/>.</summary>
internal sealed class CatalogTaskConfiguration : IEntityTypeConfiguration<CatalogTask>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CatalogTask> builder)
    {
        builder.ConfigureTaskRecord(TableNames.CatalogTask);
        builder.Property(t => t.CloseNotes).HasMaxLength(4000);
        builder.HasIndex(t => t.RequestItemId);
        builder.HasIndex(t => t.RequestId);
        builder.HasIndex(t => t.State);
        builder.HasOne<RequestItem>().WithMany().HasForeignKey(t => t.RequestItemId).OnDelete(DeleteBehavior.Cascade);
    }
}
