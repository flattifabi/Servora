using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Platform;

namespace Servora.Infrastructure.Persistence.Configurations.Platform;

/// <summary>EF Core mapping of <see cref="NumberSequence"/>.</summary>
internal sealed class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<NumberSequence> builder)
    {
        builder.ToTable("sys_number");
        builder.Property(s => s.TableName).HasMaxLength(100).IsRequired();
        builder.HasIndex(s => s.TableName).IsUnique();
        builder.Property(s => s.Prefix).HasMaxLength(16).IsRequired();
        builder.Property(s => s.Version).IsConcurrencyToken();
    }
}
