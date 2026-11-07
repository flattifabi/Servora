using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Domain.Notifications;

namespace Servora.Application.Notifications;

/// <summary>Default <see cref="INotificationInbox"/>; every query is scoped to the current user.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="clock">The clock.</param>
public sealed class NotificationInbox(IAppDbContextFactory dbFactory, ICurrentUser currentUser, IClock clock) : INotificationInbox
{
    /// <inheritdoc />
    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return 0;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<InAppNotification>().CountAsync(n => n.UserId == userId && n.ReadAt == null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedResult<InboxItem>> GetAsync(PageRequest page, bool unreadOnly = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (currentUser.UserId is not { } userId)
        {
            return PagedResult<InboxItem>.Empty();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<InAppNotification>().AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAt == null);
        }

        return await query.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Select(n => new InboxItem(n.Id, n.Title, n.Body, n.Link, n.CreatedAt, n.ReadAt != null))
            .ToPagedResultAsync(page, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result> MarkReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var notification = await db.Set<InAppNotification>().FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);
        if (notification is null)
        {
            return Result.NotFound("The notification was not found.");
        }

        notification.ReadAt ??= clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Forbidden();
        }

        var now = clock.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Set<InAppNotification>().Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var deleted = await db.Set<InAppNotification>().Where(n => n.Id == id && n.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? Result.NotFound("The notification was not found.") : Result.Success();
    }
}
