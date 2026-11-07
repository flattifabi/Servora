using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Configuration;

namespace Servora.Infrastructure.Persistence.Configurations.Configuration;

/// <summary>EF Core mapping of <see cref="StoredSecret"/>.</summary>
internal sealed class StoredSecretConfiguration : IEntityTypeConfiguration<StoredSecret>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<StoredSecret> builder)
    {
        builder.ToTable("sys_secret");
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(s => s.Name).IsUnique();
        builder.Property(s => s.Description).HasMaxLength(2000);
        builder.Property(s => s.ProtectedValue).HasMaxLength(16_000).IsRequired();
    }
}
