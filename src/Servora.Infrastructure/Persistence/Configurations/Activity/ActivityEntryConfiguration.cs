using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Activity;

namespace Servora.Infrastructure.Persistence.Configurations.Activity;

/// <summary>EF Core mapping of <see cref="ActivityEntry"/>.</summary>
internal sealed class ActivityEntryConfiguration : IEntityTypeConfiguration<ActivityEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ActivityEntry> builder)
    {
        builder.ToTable("sys_journal");
        builder.Property(a => a.TableName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Text).HasMaxLength(20_000);
        builder.Property(a => a.ChangesJson).HasMaxLength(32_000);
        builder.Property(a => a.AuthorName).HasMaxLength(256).IsRequired();
        builder.HasIndex(a => new { a.TableName, a.RecordId });
    }
}
