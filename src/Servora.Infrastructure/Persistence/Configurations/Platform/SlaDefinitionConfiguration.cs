using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Sla;

namespace Servora.Infrastructure.Persistence.Configurations.Platform;

/// <summary>EF Core mapping of <see cref="SlaDefinition"/>.</summary>
internal sealed class SlaDefinitionConfiguration : IEntityTypeConfiguration<SlaDefinition>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SlaDefinition> builder)
    {
        builder.ToTable("contract_sla");
        builder.Property(d => d.Name).HasMaxLength(150).IsRequired();
        builder.Property(d => d.Description).HasMaxLength(2000);
        builder.Property(d => d.TableName).HasMaxLength(64).IsRequired();
        builder.Property(d => d.StartConditionJson).HasMaxLength(16_000);
        builder.Property(d => d.PauseConditionJson).HasMaxLength(16_000);
        builder.Property(d => d.StopConditionJson).HasMaxLength(16_000);
        builder.Property(d => d.CancelConditionJson).HasMaxLength(16_000);
        builder.Property(d => d.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(d => d.BusinessDays).HasMaxLength(20).IsRequired();
        builder.HasIndex(d => new { d.TableName, d.IsActive });
    }
}
