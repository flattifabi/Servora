using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Knowledge;

namespace Servora.Infrastructure.Persistence.Configurations.Knowledge;

/// <summary>EF Core mapping of <see cref="KnowledgeBase"/>.</summary>
internal sealed class KnowledgeBaseConfiguration : IEntityTypeConfiguration<KnowledgeBase>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<KnowledgeBase> builder)
    {
        builder.ToTable(KnowledgeTables.KnowledgeBase);
        builder.Property(b => b.Name).HasMaxLength(150).IsRequired();
        builder.HasIndex(b => b.Name).IsUnique();
        builder.Property(b => b.Description).HasMaxLength(1000);
        builder.Property(b => b.Icon).HasMaxLength(64).IsRequired();
    }
}
