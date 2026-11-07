using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Ai;

namespace Servora.Infrastructure.Persistence.Configurations.Ai;

/// <summary>EF Core mapping of <see cref="AiToolCall"/>.</summary>
internal sealed class AiToolCallConfiguration : IEntityTypeConfiguration<AiToolCall>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AiToolCall> builder)
    {
        builder.ToTable("ai_tool_call");
        builder.Property(t => t.Kind).HasMaxLength(32).IsRequired();
        builder.Property(t => t.AgentKey).HasMaxLength(64).IsRequired();
        builder.Property(t => t.ToolName).HasMaxLength(64).IsRequired();
        builder.Property(t => t.ArgumentsJson).HasMaxLength(8000);
        builder.Property(t => t.ResultJson).HasMaxLength(16_000);
        builder.Property(t => t.ErrorMessage).HasMaxLength(1000);
        builder.Property(t => t.ActorName).HasMaxLength(256);
        builder.HasIndex(t => t.MessageId);
        builder.HasIndex(t => new { t.ConversationId, t.Status });
    }
}
