using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Platform;

namespace Servora.Infrastructure.Persistence.Configurations.Platform;

/// <summary>EF Core mapping of <see cref="CustomTable"/>.</summary>
internal sealed class CustomTableConfiguration : IEntityTypeConfiguration<CustomTable>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CustomTable> builder)
    {
        builder.ToTable("sys_db_object");
        builder.Property(t => t.Name).HasMaxLength(64).IsRequired();
        builder.HasIndex(t => t.Name).IsUnique();
        builder.Property(t => t.Label).HasMaxLength(100).IsRequired();
        builder.Property(t => t.PluralLabel).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(1000);
        builder.Property(t => t.Icon).HasMaxLength(40).IsRequired();
        builder.Property(t => t.DisplayField).HasMaxLength(64);
        builder.Property(t => t.NumberPrefix).HasMaxLength(10);
    }
}
