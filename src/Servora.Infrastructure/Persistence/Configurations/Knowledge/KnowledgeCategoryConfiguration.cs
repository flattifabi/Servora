using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Knowledge;

namespace Servora.Infrastructure.Persistence.Configurations.Knowledge;

/// <summary>EF Core mapping of <see cref="KnowledgeCategory"/>.</summary>
internal sealed class KnowledgeCategoryConfiguration : IEntityTypeConfiguration<KnowledgeCategory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<KnowledgeCategory> builder)
    {
        builder.ToTable(KnowledgeTables.Category);
        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(1000);
        builder.Property(c => c.Icon).HasMaxLength(64).IsRequired();
        builder.HasIndex(c => new { c.KnowledgeBaseId, c.Name }).IsUnique();
        builder.HasOne<KnowledgeBase>().WithMany().HasForeignKey(c => c.KnowledgeBaseId).OnDelete(DeleteBehavior.Restrict);
    }
}
