using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Changes;
using Servora.Domain.Common;
using Servora.Infrastructure.Persistence.Configurations.Itsm;

namespace Servora.Infrastructure.Persistence.Configurations.Changes;

/// <summary>EF Core mapping of <see cref="ChangeRequest"/>.</summary>
internal sealed class ChangeRequestConfiguration : IEntityTypeConfiguration<ChangeRequest>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ChangeRequest> builder)
    {
        builder.ConfigureTaskRecord(TableNames.ChangeRequest);
        builder.Property(c => c.Category).HasMaxLength(100);
        builder.Property(c => c.Justification).HasMaxLength(8000);
        builder.Property(c => c.ImplementationPlan).HasMaxLength(16_000);
        builder.Property(c => c.BackoutPlan).HasMaxLength(16_000);
        builder.Property(c => c.TestPlan).HasMaxLength(16_000);
        builder.Property(c => c.RiskAssessmentJson).HasMaxLength(4000);
        builder.Property(c => c.CloseNotes).HasMaxLength(4000);
        builder.HasIndex(c => c.State);
        builder.HasIndex(c => c.PlannedStartAt);
        builder.HasIndex(c => c.RequestedById);
    }
}
