using Servora.Application.Events;

namespace Servora.Application.Notifications;

/// <summary>Delivers notifications whose triggers match committed record events.</summary>
/// <param name="dispatcher">The notification dispatcher.</param>
public sealed class NotificationRecordEventHandler(INotificationDispatcher dispatcher) : IRecordEventHandler
{
    /// <inheritdoc />
    public int Order => 300;

    /// <inheritdoc />
    public Task HandleAsync(RecordChangedEvent recordEvent, CancellationToken cancellationToken) =>
        dispatcher.DispatchAsync(recordEvent, cancellationToken);
}
