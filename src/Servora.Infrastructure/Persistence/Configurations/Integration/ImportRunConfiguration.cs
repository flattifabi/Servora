using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Integration;

namespace Servora.Infrastructure.Persistence.Configurations.Integration;

/// <summary>EF Core mapping of <see cref="ImportRun"/>.</summary>
internal sealed class ImportRunConfiguration : IEntityTypeConfiguration<ImportRun>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ImportRun> builder)
    {
        builder.ToTable("int_import_run");
        builder.Property(r => r.SourceDescription).HasMaxLength(300);
        builder.Property(r => r.StartedBy).HasMaxLength(256);
        builder.Property(r => r.Error).HasMaxLength(2000);
        builder.HasIndex(r => new { r.ImportSetId, r.StartedAt });
        builder.HasOne<ImportSet>().WithMany().HasForeignKey(r => r.ImportSetId).OnDelete(DeleteBehavior.Cascade);
    }
}
