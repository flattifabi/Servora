using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Cmdb;

namespace Servora.Infrastructure.Persistence.Configurations.Cmdb;

/// <summary>EF Core mapping of <see cref="CiClass"/>.</summary>
internal sealed class CiClassConfiguration : IEntityTypeConfiguration<CiClass>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CiClass> builder)
    {
        builder.ToTable(CmdbTables.CiClass);
        builder.Property(c => c.Key).HasMaxLength(100).IsRequired();
        builder.HasIndex(c => c.Key).IsUnique();
        builder.Property(c => c.Label).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Icon).HasMaxLength(64).IsRequired();
        builder.Property(c => c.Color).HasMaxLength(16).IsRequired();
        builder.Property(c => c.ParentKey).HasMaxLength(100);
        builder.Property(c => c.Description).HasMaxLength(1000);
        builder.Property(c => c.AttributesJson).HasMaxLength(32_000);
    }
}
