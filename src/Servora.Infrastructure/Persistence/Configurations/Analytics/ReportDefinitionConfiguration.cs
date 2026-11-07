using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Analytics;

namespace Servora.Infrastructure.Persistence.Configurations.Analytics;

/// <summary>EF Core mapping of <see cref="ReportDefinition"/>.</summary>
internal sealed class ReportDefinitionConfiguration : IEntityTypeConfiguration<ReportDefinition>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ReportDefinition> builder)
    {
        builder.ToTable("sys_report");
        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(2000);
        builder.Property(r => r.TableName).HasMaxLength(100).IsRequired();
        builder.Property(r => r.ConditionJson).HasMaxLength(16_000);
        builder.Property(r => r.GroupBy).HasMaxLength(100);
        builder.Property(r => r.AggregateField).HasMaxLength(100);
        builder.Property(r => r.SharedWithRole).HasMaxLength(100);
        builder.Property(r => r.OwnerName).HasMaxLength(256).IsRequired();
        builder.HasIndex(r => r.OwnerId);
        builder.HasIndex(r => r.Visibility);
        builder.HasIndex(r => r.Name);
    }
}
