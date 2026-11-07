using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Common;
using Servora.Domain.Knowledge;

namespace Servora.Infrastructure.Persistence.Configurations.Knowledge;

/// <summary>EF Core mapping of <see cref="KnowledgeArticle"/>.</summary>
internal sealed class KnowledgeArticleConfiguration : IEntityTypeConfiguration<KnowledgeArticle>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<KnowledgeArticle> builder)
    {
        builder.ToTable(TableNames.KnowledgeArticle);
        builder.Property(a => a.Number).HasMaxLength(40).IsRequired();
        builder.HasIndex(a => a.Number).IsUnique();
        builder.Property(a => a.Title).HasMaxLength(250).IsRequired();
        builder.HasIndex(a => a.Title);
        builder.Property(a => a.Summary).HasMaxLength(1000);
        builder.Property(a => a.Body).HasMaxLength(100_000).IsRequired();
        builder.Property(a => a.AuthorName).HasMaxLength(256);
        builder.Property(a => a.ReviewComment).HasMaxLength(2000);
        builder.Property(a => a.Keywords).HasMaxLength(500);
        builder.Property(a => a.CustomFieldsJson).HasMaxLength(32_000);
        builder.HasIndex(a => new { a.State, a.KnowledgeBaseId });
        builder.HasIndex(a => a.CategoryId);
        builder.HasIndex(a => a.AuthorId);
        builder.HasOne<KnowledgeBase>().WithMany().HasForeignKey(a => a.KnowledgeBaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KnowledgeCategory>().WithMany().HasForeignKey(a => a.CategoryId).OnDelete(DeleteBehavior.SetNull);
    }
}
