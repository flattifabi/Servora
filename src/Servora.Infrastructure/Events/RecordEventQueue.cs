using System.Threading.Channels;
using Servora.Application.Events;

namespace Servora.Infrastructure.Events;

/// <summary>Bounded in-process queue of committed record events (singleton).</summary>
public sealed class RecordEventQueue : IRecordEventPublisher
{
    private readonly Channel<RecordChangedEvent> _channel = Channel.CreateBounded<RecordChangedEvent>(new BoundedChannelOptions(10_000)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
    });

    /// <summary>Gets the reader used by <see cref="RecordEventProcessor"/>.</summary>
    public ChannelReader<RecordChangedEvent> Reader => _channel.Reader;

    /// <summary>Gets the number of queued events (exposed as backlog metric).</summary>
    public int Count => _channel.Reader.Count;

    /// <inheritdoc />
    public ValueTask PublishAsync(RecordChangedEvent recordEvent, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(recordEvent, cancellationToken);
}
