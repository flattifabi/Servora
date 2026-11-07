using Servora.Application.Common;

namespace Servora.Application.Notifications;

/// <summary>The current user's in-app notifications (top bar bell and <c>/notifications</c>). Users only ever see their own messages.</summary>
public interface INotificationInbox
{
    /// <summary>Gets the number of unread notifications of the current user.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count (0 for non-user actors).</returns>
    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets notifications of the current user, newest first.</summary>
    /// <param name="page">The page request.</param>
    /// <param name="unreadOnly">Whether only unread notifications are returned.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page.</returns>
    Task<PagedResult<InboxItem>> GetAsync(PageRequest page, bool unreadOnly = false, CancellationToken cancellationToken = default);

    /// <summary>Marks one notification as read.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> MarkReadAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Marks all notifications of the current user as read.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> MarkAllReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Deletes one notification of the current user.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
