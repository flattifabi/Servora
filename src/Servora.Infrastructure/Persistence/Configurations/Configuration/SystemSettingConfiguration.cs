using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Configuration;

namespace Servora.Infrastructure.Persistence.Configurations.Configuration;

/// <summary>EF Core mapping of <see cref="SystemSetting"/>.</summary>
internal sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("sys_properties");
        builder.Property(s => s.Key).HasMaxLength(200).IsRequired();
        builder.HasIndex(s => s.Key).IsUnique();
        builder.Property(s => s.Value).HasMaxLength(16_000);
        builder.Property(s => s.Category).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(2000);
        builder.Property(s => s.ValueType).HasMaxLength(32).IsRequired();
    }
}
