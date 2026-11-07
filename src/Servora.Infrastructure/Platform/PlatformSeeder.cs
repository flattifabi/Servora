using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Domain.Notifications;

namespace Servora.Infrastructure.Platform;

/// <summary>Seeds the default e-mail templates of the Platform engine (idempotent).</summary>
public sealed class PlatformSeeder : IDataSeeder
{
    /// <summary>The name of the default record notification template.</summary>
    public const string DefaultTemplateName = "Record notification";

    /// <inheritdoc />
    public int Order => 150;

    /// <inheritdoc />
    public async Task SeedAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Set<NotificationTemplate>().AnyAsync(t => t.Name == DefaultTemplateName, cancellationToken))
        {
            return;
        }

        db.Set<NotificationTemplate>().Add(new NotificationTemplate
        {
            Name = DefaultTemplateName,
            Description = "Generic notification for any table: names the record, the change and the person who made it.",
            Subject = "${table} ${event}: ${Number}",
            Body = "Hello,\n\nthe ${table} record ${Number} was ${event} by ${actor}.\n\nOpen the record to see all details:\n${link}\n\nThank you,\nServora",
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
