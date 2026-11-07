using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Mcp;

namespace Servora.Infrastructure.Persistence.Configurations.Mcp;

/// <summary>EF Core mapping of <see cref="McpInvocation"/>.</summary>
internal sealed class McpInvocationConfiguration : IEntityTypeConfiguration<McpInvocation>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<McpInvocation> builder)
    {
        builder.ToTable("sys_mcp_invocation");
        builder.Property(i => i.Method).HasMaxLength(64).IsRequired();
        builder.Property(i => i.Target).HasMaxLength(256);
        builder.Property(i => i.ActorName).HasMaxLength(256);
        builder.Property(i => i.ClientName).HasMaxLength(128);
        builder.Property(i => i.ErrorMessage).HasMaxLength(1000);
        builder.Property(i => i.IpAddress).HasMaxLength(64);
        builder.HasIndex(i => i.OccurredAt);
        builder.HasIndex(i => i.ApiKeyId);
    }
}
