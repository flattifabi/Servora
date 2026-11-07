using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Automation;

namespace Servora.Infrastructure.Persistence.Configurations.Automation;

/// <summary>EF Core mapping of <see cref="FlowRun"/>.</summary>
internal sealed class FlowRunConfiguration : IEntityTypeConfiguration<FlowRun>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FlowRun> builder)
    {
        builder.ToTable("automation_flow_run");
        builder.Property(r => r.FlowName).HasMaxLength(200).IsRequired();
        builder.Property(r => r.TriggerSummary).HasMaxLength(500);
        builder.Property(r => r.TriggerTable).HasMaxLength(100);
        builder.Property(r => r.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(r => r.StartedBy).HasMaxLength(256);
        builder.Property(r => r.ContextJson).HasMaxLength(300_000).IsRequired();
        builder.Property(r => r.StepsJson).HasMaxLength(500_000).IsRequired();
        builder.Property(r => r.WaitingStepId).HasMaxLength(64);
        builder.Property(r => r.WaitReason).HasMaxLength(500);
        builder.Property(r => r.Error).HasMaxLength(2000);
        builder.HasIndex(r => new { r.FlowId, r.IdempotencyKey }).IsUnique();
        builder.HasIndex(r => new { r.Status, r.ResumeAt });
        builder.HasIndex(r => r.QueuedAt);
        builder.HasIndex(r => r.ParentRunId);
    }
}
