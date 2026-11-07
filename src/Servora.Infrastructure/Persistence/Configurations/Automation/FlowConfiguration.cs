using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Automation;

namespace Servora.Infrastructure.Persistence.Configurations.Automation;

/// <summary>EF Core mapping of <see cref="Flow"/>.</summary>
internal sealed class FlowConfiguration : IEntityTypeConfiguration<Flow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Flow> builder)
    {
        builder.ToTable("automation_flow");
        builder.Property(f => f.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(f => f.Name).IsUnique();
        builder.Property(f => f.Description).HasMaxLength(2000);
        builder.Property(f => f.TriggerTable).HasMaxLength(100);
        builder.Property(f => f.TriggerConditionJson).HasMaxLength(16_000);
        builder.Property(f => f.CronExpression).HasMaxLength(120);
        builder.Property(f => f.TimeZoneId).HasMaxLength(100);
        builder.Property(f => f.PollEndpointKey).HasMaxLength(100);
        builder.Property(f => f.PollParametersJson).HasMaxLength(8000);
        builder.Property(f => f.LastPollHash).HasMaxLength(64);
        builder.Property(f => f.InputsJson).HasMaxLength(16_000);
        builder.Property(f => f.StepsJson).HasMaxLength(500_000).IsRequired();
        builder.Property(f => f.VersionNote).HasMaxLength(500);
        builder.HasIndex(f => new { f.IsActive, f.TriggerType, f.TriggerTable });
        builder.HasIndex(f => new { f.IsActive, f.NextRunAt });
    }
}
