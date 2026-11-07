using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Platform;

namespace Servora.Infrastructure.Persistence.Configurations.Platform;

/// <summary>EF Core mapping of <see cref="CustomRecord"/>.</summary>
internal sealed class CustomRecordConfiguration : IEntityTypeConfiguration<CustomRecord>
{
    /// <summary>The maximum length of the JSON data column.</summary>
    public const int MaxDataLength = 64_000;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CustomRecord> builder)
    {
        builder.ToTable("sys_custom_record");
        builder.Property(r => r.TableName).HasMaxLength(64).IsRequired();
        builder.Property(r => r.Number).HasMaxLength(40);
        builder.Property(r => r.DisplayValue).HasMaxLength(255);
        builder.Property(r => r.DataJson).HasMaxLength(MaxDataLength).IsRequired();
        builder.HasIndex(r => new { r.TableName, r.CreatedAt });
        builder.HasIndex(r => r.Number);
        builder.HasIndex(r => new { r.TableName, r.DisplayValue });
    }
}
