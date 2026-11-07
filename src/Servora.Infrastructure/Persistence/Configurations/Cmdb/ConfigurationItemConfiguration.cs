using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Cmdb;

namespace Servora.Infrastructure.Persistence.Configurations.Cmdb;

/// <summary>EF Core mapping of <see cref="ConfigurationItem"/>.</summary>
internal sealed class ConfigurationItemConfiguration : IEntityTypeConfiguration<ConfigurationItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ConfigurationItem> builder)
    {
        builder.ToTable("cmdb_ci");
        builder.Property(c => c.Name).HasMaxLength(255).IsRequired();
        builder.HasIndex(c => c.Name);
        builder.Property(c => c.ClassName).HasMaxLength(100).IsRequired();
        builder.HasIndex(c => c.ClassName);
        builder.Property(c => c.Description).HasMaxLength(4000);
        builder.Property(c => c.Environment).HasMaxLength(64);
        builder.Property(c => c.IpAddress).HasMaxLength(64);
        builder.Property(c => c.Fqdn).HasMaxLength(255);
        builder.Property(c => c.SerialNumber).HasMaxLength(128);
        builder.Property(c => c.Manufacturer).HasMaxLength(128);
        builder.Property(c => c.Model).HasMaxLength(128);
        builder.Property(c => c.Location).HasMaxLength(128);
        builder.Property(c => c.DiscoverySource).HasMaxLength(128);
        builder.Property(c => c.CustomFieldsJson).HasMaxLength(32_000);
    }
}
