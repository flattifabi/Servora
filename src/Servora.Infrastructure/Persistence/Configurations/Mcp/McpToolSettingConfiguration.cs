using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Mcp;

namespace Servora.Infrastructure.Persistence.Configurations.Mcp;

/// <summary>EF Core mapping of <see cref="McpToolSetting"/>.</summary>
internal sealed class McpToolSettingConfiguration : IEntityTypeConfiguration<McpToolSetting>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<McpToolSetting> builder)
    {
        builder.ToTable("sys_mcp_tool");
        builder.Property(t => t.ToolName).HasMaxLength(64).IsRequired();
        builder.HasIndex(t => t.ToolName).IsUnique();
    }
}
