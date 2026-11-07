using Servora.Application.Events;

namespace Servora.Application.Notifications;

/// <summary>Evaluates notification triggers for record events and delivers matching notifications (e-mail and in-app).</summary>
public interface INotificationDispatcher
{
    /// <summary>Delivers every active notification whose trigger (table, event, condition) matches the event.</summary>
    /// <param name="recordEvent">The record event.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of notifications sent.</returns>
    Task<int> DispatchAsync(RecordChangedEvent recordEvent, CancellationToken cancellationToken = default);

    /// <summary>Delivers one notification for a record regardless of its trigger events (business rules, SLA breaches).</summary>
    /// <param name="notificationId">The notification definition.</param>
    /// <param name="recordEvent">The record event providing the record values.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of recipients.</returns>
    Task<int> SendAsync(Guid notificationId, RecordChangedEvent recordEvent, CancellationToken cancellationToken = default);
}
