namespace Servora.Application.Events;

/// <summary>
/// Queues committed record events for asynchronous processing by <see cref="IRecordEventHandler"/>s.
/// The queue is bounded; producers wait briefly when it is full instead of dropping events.
/// </summary>
public interface IRecordEventPublisher
{
    /// <summary>Queues an event.</summary>
    /// <param name="recordEvent">The event.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the event is queued.</returns>
    ValueTask PublishAsync(RecordChangedEvent recordEvent, CancellationToken cancellationToken = default);
}
