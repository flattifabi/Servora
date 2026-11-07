using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Platform;

namespace Servora.Infrastructure.Persistence.Configurations.Platform;

/// <summary>EF Core mapping of <see cref="CustomField"/>.</summary>
internal sealed class CustomFieldConfiguration : IEntityTypeConfiguration<CustomField>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CustomField> builder)
    {
        builder.ToTable("sys_dictionary");
        builder.Property(f => f.TableName).HasMaxLength(64).IsRequired();
        builder.Property(f => f.Name).HasMaxLength(64).IsRequired();
        builder.HasIndex(f => new { f.TableName, f.Name }).IsUnique();
        builder.Property(f => f.Label).HasMaxLength(100).IsRequired();
        builder.Property(f => f.ChoicesJson).HasMaxLength(16_000);
        builder.Property(f => f.ReferenceTable).HasMaxLength(64);
        builder.Property(f => f.DefaultValue).HasMaxLength(1000);
        builder.Property(f => f.HelpText).HasMaxLength(1000);
    }
}
