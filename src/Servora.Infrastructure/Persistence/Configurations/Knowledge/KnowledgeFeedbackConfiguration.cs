using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Knowledge;

namespace Servora.Infrastructure.Persistence.Configurations.Knowledge;

/// <summary>EF Core mapping of <see cref="KnowledgeFeedback"/>.</summary>
internal sealed class KnowledgeFeedbackConfiguration : IEntityTypeConfiguration<KnowledgeFeedback>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<KnowledgeFeedback> builder)
    {
        builder.ToTable(KnowledgeTables.Feedback);
        builder.Property(f => f.Comment).HasMaxLength(1000);
        builder.HasIndex(f => new { f.ArticleId, f.UserId }).IsUnique();
        builder.HasOne<KnowledgeArticle>().WithMany().HasForeignKey(f => f.ArticleId).OnDelete(DeleteBehavior.Cascade);
    }
}
