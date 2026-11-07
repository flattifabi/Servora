using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Integration;

namespace Servora.Infrastructure.Persistence.Configurations.Integration;

/// <summary>EF Core mapping of <see cref="ImportRow"/>.</summary>
internal sealed class ImportRowConfiguration : IEntityTypeConfiguration<ImportRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ImportRow> builder)
    {
        builder.ToTable("int_import_row");
        builder.Property(r => r.DataJson).HasMaxLength(8000).IsRequired();
        builder.Property(r => r.Message).HasMaxLength(1000);
        builder.HasIndex(r => new { r.RunId, r.RowNumber });
        builder.HasOne<ImportRun>().WithMany().HasForeignKey(r => r.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}
