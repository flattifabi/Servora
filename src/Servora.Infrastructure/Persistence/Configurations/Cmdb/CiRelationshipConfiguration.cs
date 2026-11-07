using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Cmdb;

namespace Servora.Infrastructure.Persistence.Configurations.Cmdb;

/// <summary>EF Core mapping of <see cref="CiRelationship"/>.</summary>
internal sealed class CiRelationshipConfiguration : IEntityTypeConfiguration<CiRelationship>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CiRelationship> builder)
    {
        builder.ToTable(CmdbTables.Relationship);
        builder.Property(r => r.Note).HasMaxLength(500);
        builder.HasIndex(r => new { r.ParentId, r.ChildId, r.Type }).IsUnique();
        builder.HasIndex(r => r.ChildId);

        // Both foreign keys restrict deletes; the CMDB service removes relationships explicitly before deleting a CI
        // (SQL Server does not allow two cascading paths to the same table).
        builder.HasOne<ConfigurationItem>().WithMany().HasForeignKey(r => r.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConfigurationItem>().WithMany().HasForeignKey(r => r.ChildId).OnDelete(DeleteBehavior.Restrict);
    }
}
