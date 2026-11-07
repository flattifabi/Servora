using System.Collections.Concurrent;
using System.Threading.Channels;
using Servora.Application.Automation.Engine;

namespace Servora.Infrastructure.Automation;

/// <summary>
/// Singleton <see cref="IFlowRunQueue"/> backed by a bounded channel. Tracks queued / executing runs so a run is never
/// executed twice concurrently, and holds the cancellation sources of executing runs.
/// </summary>
public sealed class FlowRunQueue : IFlowRunQueue
{
    /// <summary>The channel capacity; overflow is picked up by the periodic sweep of queued runs.</summary>
    public const int Capacity = 2000;

    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });
    private readonly ConcurrentDictionary<Guid, byte> _pending = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _executing = new();

    /// <summary>Gets the reader used by the engine worker.</summary>
    public ChannelReader<Guid> Reader => _channel.Reader;

    /// <summary>Gets the number of queued or executing runs.</summary>
    public int InFlight => _pending.Count;

    /// <inheritdoc />
    public bool Enqueue(Guid runId)
    {
        if (!_pending.TryAdd(runId, 0))
        {
            return false;
        }

        if (_channel.Writer.TryWrite(runId))
        {
            return true;
        }

        _pending.TryRemove(runId, out _);
        return false;
    }

    /// <inheritdoc />
    public void SignalCancel(Guid runId)
    {
        if (_executing.TryGetValue(runId, out var source))
        {
            try
            {
                source.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The run finished in the meantime.
            }
        }
    }

    /// <summary>Registers an executing run and returns its cancellation token source.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="stoppingToken">The host stopping token.</param>
    /// <returns>The linked cancellation source.</returns>
    internal CancellationTokenSource BeginExecution(Guid runId, CancellationToken stoppingToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _executing[runId] = source;
        return source;
    }

    /// <summary>Unregisters a finished run.</summary>
    /// <param name="runId">The run.</param>
    internal void EndExecution(Guid runId)
    {
        if (_executing.TryRemove(runId, out var source))
        {
            source.Dispose();
        }

        _pending.TryRemove(runId, out _);
    }
}
