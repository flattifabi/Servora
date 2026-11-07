using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Knowledge;

namespace Servora.Infrastructure.Persistence.Configurations.Knowledge;

/// <summary>EF Core mapping of <see cref="KnowledgeArticleVersion"/>.</summary>
internal sealed class KnowledgeArticleVersionConfiguration : IEntityTypeConfiguration<KnowledgeArticleVersion>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<KnowledgeArticleVersion> builder)
    {
        builder.ToTable(KnowledgeTables.Version);
        builder.Property(v => v.Title).HasMaxLength(250).IsRequired();
        builder.Property(v => v.Summary).HasMaxLength(1000);
        builder.Property(v => v.Body).HasMaxLength(100_000).IsRequired();
        builder.Property(v => v.ChangeNote).HasMaxLength(500);
        builder.Property(v => v.CreatedBy).HasMaxLength(256);
        builder.HasIndex(v => new { v.ArticleId, v.VersionNumber });
        builder.HasOne<KnowledgeArticle>().WithMany().HasForeignKey(v => v.ArticleId).OnDelete(DeleteBehavior.Cascade);
    }
}
