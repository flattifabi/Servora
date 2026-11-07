using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Api;

namespace Servora.Infrastructure.Persistence.Configurations.Api;

/// <summary>EF Core mapping of <see cref="ApiKey"/>.</summary>
internal sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("sys_api_key");
        builder.Property(k => k.Name).HasMaxLength(100).IsRequired();
        builder.Property(k => k.Description).HasMaxLength(1000);
        builder.Property(k => k.KeyId).HasMaxLength(32).IsRequired();
        builder.HasIndex(k => k.KeyId).IsUnique();
        builder.Property(k => k.SecretHash).HasMaxLength(64).IsRequired();
        builder.Property(k => k.ServiceIdentity).HasMaxLength(100);
        builder.Property(k => k.Scopes).HasMaxLength(8000).IsRequired();
        builder.Property(k => k.AllowedIpRanges).HasMaxLength(2000);
        builder.Property(k => k.RevokedBy).HasMaxLength(256);
        builder.Property(k => k.RevocationReason).HasMaxLength(500);
        builder.Property(k => k.LastUsedIp).HasMaxLength(64);
        builder.HasIndex(k => k.OwnerUserId);
        builder.HasIndex(k => k.Name);
    }
}
