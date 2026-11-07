using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Portal;

namespace Servora.Application.Portal;

/// <summary>Default <see cref="IPortalAnnouncementService"/> implementation.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="audit">The audit service.</param>
/// <param name="clock">The clock.</param>
public sealed class PortalAnnouncementService(IAppDbContextFactory dbFactory, ICurrentUser currentUser, IAuditService audit, IClock clock) : IPortalAnnouncementService
{
    private const int MaxLive = 10;
    private const int MaxListed = 200;

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<PortalAnnouncementView>>> GetLiveAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Portal.Access, cancellationToken))
        {
            return Result<IReadOnlyList<PortalAnnouncementView>>.Forbidden();
        }

        var now = clock.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Set<PortalAnnouncement>().AsNoTracking()
            .Where(a => a.IsActive && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
            .OrderBy(a => a.Order).ThenByDescending(a => a.StartsAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        IReadOnlyList<PortalAnnouncementView> live = rows
            .OrderBy(a => a.Kind == PortalAnnouncementKind.Outage ? 0 : 1)
            .ThenBy(a => a.Order)
            .ThenByDescending(a => a.StartsAt)
            .Take(MaxLive)
            .Select(a => ToView(a, now))
            .ToList();
        return Result<IReadOnlyList<PortalAnnouncementView>>.Success(live);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<PortalAnnouncementView>>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Portal.Manage, cancellationToken))
        {
            return Result<IReadOnlyList<PortalAnnouncementView>>.Forbidden();
        }

        var now = clock.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Set<PortalAnnouncement>().AsNoTracking()
            .OrderByDescending(a => a.StartsAt)
            .Take(MaxListed)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<PortalAnnouncementView>>.Success(rows.Select(a => ToView(a, now)).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> SaveAsync(Guid? id, PortalAnnouncementInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!await currentUser.HasPermissionAsync(Permissions.Portal.Manage, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        if (input.Validate() is { } error)
        {
            return Result<Guid>.Invalid(error);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        PortalAnnouncement announcement;
        if (id is { } existingId)
        {
            var found = await db.Set<PortalAnnouncement>().FirstOrDefaultAsync(a => a.Id == existingId, cancellationToken);
            if (found is null)
            {
                return Result<Guid>.NotFound("The announcement was not found.");
            }

            if (found.Version != input.Version)
            {
                return Result<Guid>.Conflict("The announcement was changed by someone else in the meantime. Reload it and apply your changes again.");
            }

            announcement = found;
        }
        else
        {
            announcement = new PortalAnnouncement();
            db.Set<PortalAnnouncement>().Add(announcement);
        }

        announcement.Title = input.Title.Trim();
        announcement.Body = string.IsNullOrWhiteSpace(input.Body) ? null : input.Body.Trim();
        announcement.Kind = input.Kind;
        announcement.LinkUrl = string.IsNullOrWhiteSpace(input.LinkUrl) ? null : input.LinkUrl.Trim();
        announcement.LinkText = announcement.LinkUrl is null || string.IsNullOrWhiteSpace(input.LinkText) ? null : input.LinkText.Trim();
        announcement.StartsAt = input.StartsAt;
        announcement.EndsAt = input.EndsAt;
        announcement.IsActive = input.IsActive;
        announcement.Order = input.Order;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<Guid>.Conflict("The announcement was changed by someone else in the meantime. Reload it and apply your changes again.");
        }

        await audit.WriteAsync(new AuditEntry("portal", id is null ? "announcement.created" : "announcement.updated")
        {
            TargetType = PortalTables.Announcement,
            TargetId = announcement.Id.ToString(),
            TargetName = announcement.Title,
        }, cancellationToken);
        return Result<Guid>.Success(announcement.Id);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Portal.Manage, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var announcement = await db.Set<PortalAnnouncement>().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (announcement is null)
        {
            return Result.NotFound("The announcement was not found.");
        }

        db.Set<PortalAnnouncement>().Remove(announcement);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry("portal", "announcement.deleted")
        {
            TargetType = PortalTables.Announcement,
            TargetId = id.ToString(),
            TargetName = announcement.Title,
        }, cancellationToken);
        return Result.Success();
    }

    private static PortalAnnouncementView ToView(PortalAnnouncement a, DateTimeOffset now) => new()
    {
        Id = a.Id,
        Title = a.Title,
        Body = a.Body,
        Kind = a.Kind,
        LinkUrl = PortalLinkPolicy.IsAllowed(a.LinkUrl) ? a.LinkUrl : null,
        LinkText = a.LinkText,
        StartsAt = a.StartsAt,
        EndsAt = a.EndsAt,
        IsActive = a.IsActive,
        Order = a.Order,
        IsLive = a.IsActive && a.StartsAt <= now && (a.EndsAt is null || a.EndsAt > now),
        Version = a.Version,
        UpdatedAt = a.UpdatedAt,
        UpdatedBy = a.UpdatedBy,
    };
}
