using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Automation;

namespace Servora.Infrastructure.Persistence.Configurations.Automation;

/// <summary>EF Core mapping of <see cref="FlowRunStep"/>.</summary>
internal sealed class FlowRunStepConfiguration : IEntityTypeConfiguration<FlowRunStep>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FlowRunStep> builder)
    {
        builder.ToTable("automation_flow_run_step");
        builder.Property(s => s.StepId).HasMaxLength(64).IsRequired();
        builder.Property(s => s.StepKey).HasMaxLength(64).IsRequired();
        builder.Property(s => s.StepName).HasMaxLength(200).IsRequired();
        builder.Property(s => s.ActionType).HasMaxLength(100).IsRequired();
        builder.Property(s => s.InputJson).HasMaxLength(20_000);
        builder.Property(s => s.OutputJson).HasMaxLength(20_000);
        builder.Property(s => s.Error).HasMaxLength(2000);
        builder.HasIndex(s => new { s.RunId, s.Sequence });
    }
}
