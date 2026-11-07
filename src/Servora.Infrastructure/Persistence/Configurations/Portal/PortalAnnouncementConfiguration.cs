using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Portal;

namespace Servora.Infrastructure.Persistence.Configurations.Portal;

/// <summary>EF Core mapping of <see cref="PortalAnnouncement"/>.</summary>
internal sealed class PortalAnnouncementConfiguration : IEntityTypeConfiguration<PortalAnnouncement>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PortalAnnouncement> builder)
    {
        builder.ToTable(PortalTables.Announcement);
        builder.Property(a => a.Title).HasMaxLength(PortalAnnouncement.TitleMaxLength).IsRequired();
        builder.Property(a => a.Body).HasMaxLength(PortalAnnouncement.BodyMaxLength);
        builder.Property(a => a.LinkUrl).HasMaxLength(PortalAnnouncement.LinkMaxLength);
        builder.Property(a => a.LinkText).HasMaxLength(PortalAnnouncement.LinkTextMaxLength);
        builder.HasIndex(a => new { a.IsActive, a.StartsAt });
    }
}
