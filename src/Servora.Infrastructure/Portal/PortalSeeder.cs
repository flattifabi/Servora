using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Domain.Configuration;
using Servora.Domain.Portal;

namespace Servora.Infrastructure.Portal;

/// <summary>
/// Seeds a welcome announcement for the self-service portal once (guarded by the system setting <see cref="MarkerKey"/>,
/// so a deleted announcement is not recreated).
/// </summary>
public sealed class PortalSeeder : IDataSeeder
{
    /// <summary>The system setting that records that the portal demo data was seeded.</summary>
    public const string MarkerKey = "portal.demo_data.seeded";

    /// <inheritdoc />
    public int Order => 600;

    /// <inheritdoc />
    public bool IsDemoData => true;

    /// <inheritdoc />
    public async Task SeedAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Set<SystemSetting>().AnyAsync(s => s.Key == MarkerKey, cancellationToken))
        {
            return;
        }

        if (!await db.Set<PortalAnnouncement>().AnyAsync(cancellationToken))
        {
            db.Set<PortalAnnouncement>().Add(new PortalAnnouncement
            {
                Title = "Welcome to the new Self-Service Portal",
                Body = "Order hardware, software and access, report issues and find answers in the knowledge base – all in one place. Track everything you submitted under \"My items\".",
                Kind = PortalAnnouncementKind.Success,
                LinkUrl = "/portal/knowledge",
                LinkText = "Browse the knowledge base",
                StartsAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                IsActive = true,
                Order = 100,
            });
        }

        db.Set<SystemSetting>().Add(new SystemSetting
        {
            Key = MarkerKey,
            Value = "true",
            Category = "Portal",
            ValueType = "bool",
            Description = "The portal welcome announcement was seeded (it is not recreated after deletion).",
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
