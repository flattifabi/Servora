using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Ai;

namespace Servora.Infrastructure.Persistence.Configurations.Ai;

/// <summary>EF Core mapping of <see cref="AiConversation"/>.</summary>
internal sealed class AiConversationConfiguration : IEntityTypeConfiguration<AiConversation>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AiConversation> builder)
    {
        builder.ToTable("ai_conversation");
        builder.Property(c => c.Title).HasMaxLength(200).IsRequired();
        builder.HasIndex(c => new { c.UserId, c.LastMessageAt });
    }
}
