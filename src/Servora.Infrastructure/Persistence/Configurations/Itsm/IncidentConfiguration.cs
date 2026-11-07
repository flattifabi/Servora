using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Common;
using Servora.Domain.Itsm;

namespace Servora.Infrastructure.Persistence.Configurations.Itsm;

/// <summary>EF Core mapping of <see cref="Incident"/> (table <c>incident</c>).</summary>
public sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ConfigureTaskRecord(TableNames.Incident);
        builder.Property(i => i.Category).HasMaxLength(100);
        builder.Property(i => i.Subcategory).HasMaxLength(100);
        builder.Property(i => i.ResolutionNotes).HasMaxLength(8000);
        builder.HasIndex(i => new { i.Active, i.State });
        builder.HasIndex(i => i.CallerId);
        builder.HasIndex(i => i.ProblemId);
        builder.HasIndex(i => i.ParentIncidentId);
        builder.HasIndex(i => i.OpenedAt);
        builder.HasIndex(i => i.ResolvedAt);
        builder.HasIndex(i => i.UpdatedAt);
    }
}
