using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Platform;

namespace Servora.Infrastructure.Persistence.Configurations.Platform;

/// <summary>EF Core mapping of <see cref="TableLayout"/>.</summary>
internal sealed class TableLayoutConfiguration : IEntityTypeConfiguration<TableLayout>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TableLayout> builder)
    {
        builder.ToTable("sys_ui_layout");
        builder.Property(l => l.TableName).HasMaxLength(64).IsRequired();
        builder.HasIndex(l => l.TableName).IsUnique();
        builder.Property(l => l.FormJson).HasMaxLength(16_000);
        builder.Property(l => l.ListColumnsJson).HasMaxLength(4000);
    }
}
