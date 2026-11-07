using System.Collections.Concurrent;
using Servora.Application.Events;

namespace Servora.Tests.Unit.Platform;

/// <summary>Captures published record events so tests can feed them into handlers deterministically.</summary>
public sealed class TestEventPublisher : IRecordEventPublisher
{
    /// <summary>Gets the captured events.</summary>
    public ConcurrentQueue<RecordChangedEvent> Events { get; } = new();

    /// <inheritdoc />
    public ValueTask PublishAsync(RecordChangedEvent recordEvent, CancellationToken cancellationToken = default)
    {
        Events.Enqueue(recordEvent);
        return ValueTask.CompletedTask;
    }
}
