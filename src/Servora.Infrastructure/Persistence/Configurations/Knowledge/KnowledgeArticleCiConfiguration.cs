using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Cmdb;
using Servora.Domain.Knowledge;

namespace Servora.Infrastructure.Persistence.Configurations.Knowledge;

/// <summary>EF Core mapping of <see cref="KnowledgeArticleCi"/>.</summary>
internal sealed class KnowledgeArticleCiConfiguration : IEntityTypeConfiguration<KnowledgeArticleCi>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<KnowledgeArticleCi> builder)
    {
        builder.ToTable(KnowledgeTables.ArticleCi);
        builder.HasIndex(l => new { l.ArticleId, l.ConfigurationItemId }).IsUnique();
        builder.HasIndex(l => l.ConfigurationItemId);
        builder.HasOne<KnowledgeArticle>().WithMany().HasForeignKey(l => l.ArticleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ConfigurationItem>().WithMany().HasForeignKey(l => l.ConfigurationItemId).OnDelete(DeleteBehavior.Cascade);
    }
}
