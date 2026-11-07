using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Observability;

namespace Servora.Infrastructure.Persistence.Configurations.Observability;

/// <summary>EF Core mapping of <see cref="AuditEvent"/>.</summary>
internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("sys_audit_event");
        builder.Property(a => a.Category).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(128).IsRequired();
        builder.Property(a => a.ActorId).HasMaxLength(64);
        builder.Property(a => a.ActorName).HasMaxLength(256).IsRequired();
        builder.Property(a => a.ActorType).HasMaxLength(32).IsRequired();
        builder.Property(a => a.TargetType).HasMaxLength(100);
        builder.Property(a => a.TargetId).HasMaxLength(64);
        builder.Property(a => a.TargetName).HasMaxLength(256);
        builder.Property(a => a.Message).HasMaxLength(2000);
        builder.Property(a => a.DetailsJson).HasMaxLength(16_000);
        builder.Property(a => a.IpAddress).HasMaxLength(64);
        builder.Property(a => a.CorrelationId).HasMaxLength(64);
        builder.Property(a => a.PreviousHash).HasMaxLength(64);
        builder.Property(a => a.Hash).HasMaxLength(64);
        builder.HasIndex(a => a.OccurredAt);
        builder.HasIndex(a => new { a.Category, a.Action });
    }
}
