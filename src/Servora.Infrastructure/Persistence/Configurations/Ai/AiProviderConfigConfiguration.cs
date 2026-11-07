using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Ai;

namespace Servora.Infrastructure.Persistence.Configurations.Ai;

/// <summary>EF Core mapping of <see cref="AiProviderConfig"/>.</summary>
internal sealed class AiProviderConfigConfiguration : IEntityTypeConfiguration<AiProviderConfig>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AiProviderConfig> builder)
    {
        builder.ToTable("ai_provider");
        builder.Ignore(p => p.SecretName);
        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.BaseUrl).HasMaxLength(500).IsRequired();
        builder.Property(p => p.Model).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Deployment).HasMaxLength(200);
        builder.Property(p => p.ApiVersion).HasMaxLength(50);
        builder.Property(p => p.LastTestMessage).HasMaxLength(1000);
        builder.HasIndex(p => p.Name).IsUnique();
    }
}
