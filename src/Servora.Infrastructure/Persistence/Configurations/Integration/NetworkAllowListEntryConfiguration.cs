using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Integration;

namespace Servora.Infrastructure.Persistence.Configurations.Integration;

/// <summary>EF Core mapping of <see cref="NetworkAllowListEntry"/>.</summary>
internal sealed class NetworkAllowListEntryConfiguration : IEntityTypeConfiguration<NetworkAllowListEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<NetworkAllowListEntry> builder)
    {
        builder.ToTable("int_network_allow_list");
        builder.Property(e => e.Cidr).HasMaxLength(64).IsRequired();
        builder.HasIndex(e => e.Cidr).IsUnique();
        builder.Property(e => e.Description).HasMaxLength(500);
    }
}
