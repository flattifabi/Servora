using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Ai;

namespace Servora.Infrastructure.Persistence.Configurations.Ai;

/// <summary>EF Core mapping of <see cref="AiAgentSetting"/>.</summary>
internal sealed class AiAgentSettingConfiguration : IEntityTypeConfiguration<AiAgentSetting>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AiAgentSetting> builder)
    {
        builder.ToTable("ai_agent");
        builder.Property(a => a.AgentKey).HasMaxLength(64).IsRequired();
        builder.HasIndex(a => a.AgentKey).IsUnique();
    }
}
