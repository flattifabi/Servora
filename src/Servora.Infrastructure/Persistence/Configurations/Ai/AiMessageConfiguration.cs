using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Ai;

namespace Servora.Infrastructure.Persistence.Configurations.Ai;

/// <summary>EF Core mapping of <see cref="AiMessage"/>.</summary>
internal sealed class AiMessageConfiguration : IEntityTypeConfiguration<AiMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AiMessage> builder)
    {
        builder.ToTable("ai_message");
        builder.Property(m => m.Content).HasMaxLength(32_000).IsRequired();
        builder.Property(m => m.AgentKey).HasMaxLength(64);
        builder.HasIndex(m => new { m.ConversationId, m.CreatedAt });
    }
}
