using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Platform;

namespace Servora.Infrastructure.Persistence.Configurations.Platform;

/// <summary>EF Core mapping of <see cref="RecordFieldValue"/>.</summary>
internal sealed class RecordFieldValueConfiguration : IEntityTypeConfiguration<RecordFieldValue>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RecordFieldValue> builder)
    {
        builder.ToTable("sys_record_field_value");
        builder.Property(v => v.TableName).HasMaxLength(64).IsRequired();
        builder.Property(v => v.Field).HasMaxLength(64).IsRequired();
        builder.Property(v => v.TextValue).HasMaxLength(RecordFieldValue.MaxTextLength);
        builder.HasIndex(v => new { v.TableName, v.RecordId, v.Field }).IsUnique();
        builder.HasIndex(v => new { v.TableName, v.Field, v.TextValue });
        builder.HasIndex(v => new { v.TableName, v.Field, v.NumberValue });
        builder.HasIndex(v => new { v.TableName, v.Field, v.DateValue });
    }
}
