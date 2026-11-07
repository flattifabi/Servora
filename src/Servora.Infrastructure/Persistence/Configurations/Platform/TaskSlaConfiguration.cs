using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Sla;

namespace Servora.Infrastructure.Persistence.Configurations.Platform;

/// <summary>EF Core mapping of <see cref="TaskSla"/>.</summary>
internal sealed class TaskSlaConfiguration : IEntityTypeConfiguration<TaskSla>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TaskSla> builder)
    {
        builder.ToTable("task_sla");
        builder.Property(t => t.TableName).HasMaxLength(64).IsRequired();
        builder.Property(t => t.RecordDisplay).HasMaxLength(255);
        builder.HasIndex(t => new { t.TableName, t.RecordId });
        builder.HasIndex(t => new { t.Stage, t.HasBreached, t.DueAt });
        builder.HasIndex(t => t.SlaDefinitionId);
    }
}
