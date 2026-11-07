using Microsoft.Extensions.Logging;
using Servora.Application.Observability;

namespace Servora.Infrastructure.Observability.Logging;

/// <summary>
/// Fixed-size ring buffer holding the most recent log entries for the log viewer (live tail). Thread-safe; appending is
/// O(1) and never allocates beyond the entry itself.
/// </summary>
public sealed class InMemoryLogSink : ILogStore
{
    private readonly LogEntry?[] _buffer;
    private readonly object _sync = new();
    private long _sequence;
    private long _totalWritten;
    private int _next;
    private int _count;

    /// <summary>Initializes a new instance of the <see cref="InMemoryLogSink"/> class.</summary>
    /// <param name="capacity">The number of entries kept (100..100,000).</param>
    public InMemoryLogSink(int capacity = 5000)
    {
        _buffer = new LogEntry?[Math.Clamp(capacity, 100, 100_000)];
    }

    /// <summary>Gets the capacity.</summary>
    public int Capacity => _buffer.Length;

    /// <summary>Reserves the next sequence number for a new entry.</summary>
    /// <returns>The sequence number.</returns>
    public long NextSequence() => Interlocked.Increment(ref _sequence);

    /// <summary>Appends an entry, evicting the oldest one when the buffer is full.</summary>
    /// <param name="entry">The entry.</param>
    public void Append(LogEntry entry)
    {
        lock (_sync)
        {
            _buffer[_next] = entry;
            _next = (_next + 1) % _buffer.Length;
            _count = Math.Min(_count + 1, _buffer.Length);
            _totalWritten++;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<LogEntry> Query(LogQuery query)
    {
        var result = new List<LogEntry>(Math.Min(query.Take, 256));
        foreach (var entry in Snapshot())
        {
            if (Matches(entry, query))
            {
                result.Add(entry);
                if (result.Count >= query.Take)
                {
                    break;
                }
            }
        }

        return result;
    }

    /// <inheritdoc />
    public LogEntry? Get(long sequence) => Snapshot().FirstOrDefault(e => e.Sequence == sequence);

    /// <inheritdoc />
    public LogStoreStatistics GetStatistics()
    {
        var entries = Snapshot();
        long total;
        lock (_sync)
        {
            total = _totalWritten;
        }

        return new LogStoreStatistics(
            _buffer.Length,
            entries.Count,
            total,
            entries.Count == 0 ? 0 : entries[0].Sequence,
            entries.Count(e => e.Level >= LogLevel.Error && e.Level != LogLevel.None),
            entries.Count(e => e.Level == LogLevel.Warning),
            entries.Count(e => e.Level <= LogLevel.Debug));
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetCategories(int max = 200) =>
        Snapshot().Select(e => e.Category).Distinct(StringComparer.Ordinal).Order(StringComparer.OrdinalIgnoreCase).Take(Math.Clamp(max, 1, 1000)).ToList();

    /// <summary>Checks whether an entry matches a query.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="query">The query.</param>
    /// <returns><see langword="true"/> when it matches.</returns>
    public static bool Matches(LogEntry entry, LogQuery query)
    {
        if (query.AfterSequence is { } after && entry.Sequence <= after)
        {
            return false;
        }

        if (query.MinLevel is { } level && entry.Level < level)
        {
            return false;
        }

        if (query.From is { } from && entry.Timestamp < from)
        {
            return false;
        }

        if (query.To is { } to && entry.Timestamp > to)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.Category) && !entry.Category.Contains(query.Category.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.CorrelationId) && !string.Equals(entry.CorrelationId, query.CorrelationId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            return entry.Message.Contains(term, StringComparison.OrdinalIgnoreCase)
                || entry.Category.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (entry.Exception?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (entry.CorrelationId?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        return true;
    }

    /// <summary>Copies the buffered entries, newest first.</summary>
    private List<LogEntry> Snapshot()
    {
        lock (_sync)
        {
            var list = new List<LogEntry>(_count);
            for (var i = 1; i <= _count; i++)
            {
                var index = (_next - i + _buffer.Length) % _buffer.Length;
                if (_buffer[index] is { } entry)
                {
                    list.Add(entry);
                }
            }

            return list;
        }
    }
}
