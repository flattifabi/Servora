using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Itsm;

namespace Servora.Infrastructure.Persistence.Configurations.Itsm;

/// <summary>EF Core mapping of <see cref="IncidentCategory"/> (table <c>incident_category</c>).</summary>
public sealed class IncidentCategoryConfiguration : IEntityTypeConfiguration<IncidentCategory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IncidentCategory> builder)
    {
        builder.ToTable(IncidentCategory.TableName);
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(c => new { c.ParentId, c.Name }).IsUnique();
    }
}
