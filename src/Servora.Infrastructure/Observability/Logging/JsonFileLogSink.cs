using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Servora.Application.Observability;

namespace Servora.Infrastructure.Observability.Logging;

/// <summary>
/// Writes log entries as JSON lines to daily rolling files (<c>servora-yyyyMMdd.jsonl</c>, continued as
/// <c>servora-yyyyMMdd-001.jsonl</c> … when the size cap is reached) and deletes files older than the retention period.
/// Writing happens on a background task through a bounded queue, so logging never blocks request threads; when the
/// queue is full new entries are dropped and counted.
/// </summary>
public sealed partial class JsonFileLogSink : ILogFileStore, IDisposable
{
    private const string FilePrefix = "servora-";
    private const string FileExtension = ".jsonl";
    private static readonly byte[] s_newLine = "\n"u8.ToArray();

    private readonly Channel<LogEntry> _queue;
    private readonly Task _writerTask;
    private readonly object _fileSync = new();
    private LogRetentionSettings _retention;
    private FileStream? _stream;
    private string? _currentDay;
    private int _currentPart;
    private long _dropped;

    /// <summary>Initializes a new instance of the <see cref="JsonFileLogSink"/> class.</summary>
    /// <param name="directory">The log directory (created when missing).</param>
    /// <param name="retention">The initial retention settings.</param>
    /// <param name="queueCapacity">The maximum number of queued entries.</param>
    public JsonFileLogSink(string directory, LogRetentionSettings? retention = null, int queueCapacity = 20_000)
    {
        Directory = directory;
        System.IO.Directory.CreateDirectory(directory);
        _retention = retention ?? new LogRetentionSettings();
        _queue = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(Math.Max(100, queueCapacity))
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });
        _writerTask = Task.Run(WriteLoopAsync);
    }

    /// <inheritdoc />
    public string Directory { get; }

    /// <inheritdoc />
    public LogRetentionSettings Retention => _retention;

    /// <summary>Gets the number of entries dropped because the queue was full.</summary>
    public long Dropped => Interlocked.Read(ref _dropped);

    /// <summary>Queues an entry for writing.</summary>
    /// <param name="entry">The entry.</param>
    public void Enqueue(LogEntry entry)
    {
        if (!_queue.Writer.TryWrite(entry))
        {
            Interlocked.Increment(ref _dropped);
        }
    }

    /// <summary>Waits until all queued entries are written (used by tests and on shutdown).</summary>
    /// <param name="timeout">The maximum wait time.</param>
    /// <returns>A task that completes when the queue is drained or the timeout elapsed.</returns>
    public async Task FlushAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (_queue.Reader.Count > 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        lock (_fileSync)
        {
            _stream?.Flush(flushToDisk: false);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<LogFileInfo> ListFiles()
    {
        try
        {
            return new DirectoryInfo(Directory).EnumerateFiles(FilePrefix + "*" + FileExtension)
                .Where(f => IsLogFileName(f.Name))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ThenByDescending(f => f.Name, StringComparer.Ordinal)
                .Take(500)
                .Select(f => new LogFileInfo(f.Name, f.Length, new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero)))
                .ToList();
        }
        catch (IOException)
        {
            return [];
        }
    }

    /// <inheritdoc />
    public Stream? OpenRead(string name)
    {
        if (!IsLogFileName(name))
        {
            return null;
        }

        var path = Path.Combine(Directory, name);
        if (!File.Exists(path))
        {
            return null;
        }

        lock (_fileSync)
        {
            _stream?.Flush(flushToDisk: false);
        }

        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    }

    /// <inheritdoc />
    public void Configure(LogRetentionSettings settings)
    {
        if (settings.Validate() is null)
        {
            _retention = settings;
        }
    }

    /// <inheritdoc />
    public int ApplyRetention()
    {
        var cutoff = DateTime.UtcNow.AddDays(-_retention.RetentionDays);
        var deleted = 0;
        string? active;
        lock (_fileSync)
        {
            active = _stream?.Name;
        }

        foreach (var file in new DirectoryInfo(Directory).EnumerateFiles(FilePrefix + "*" + FileExtension))
        {
            if (!IsLogFileName(file.Name) || file.LastWriteTimeUtc >= cutoff || string.Equals(file.FullName, active, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                file.Delete();
                deleted++;
            }
            catch (IOException)
            {
                // Locked by another process (for example an editor); retried at the next run.
            }
            catch (UnauthorizedAccessException)
            {
                // No permission; retried at the next run.
            }
        }

        return deleted;
    }

    /// <summary>Checks that a name is a plain Servora log file name (no directories, no traversal).</summary>
    /// <param name="name">The file name.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    public static bool IsLogFileName(string? name) => name is not null && LogFileName().IsMatch(name);

    /// <summary>Serializes an entry as one JSON line.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The UTF-8 encoded JSON (without newline).</returns>
    public static byte[] Serialize(LogEntry entry)
    {
        using var buffer = new MemoryStream(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("ts", entry.Timestamp.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteNumber("seq", entry.Sequence);
            writer.WriteString("level", entry.Level.ToString());
            writer.WriteString("category", entry.Category);
            if (entry.EventId != 0)
            {
                writer.WriteNumber("eventId", entry.EventId);
            }

            if (!string.IsNullOrEmpty(entry.EventName))
            {
                writer.WriteString("eventName", entry.EventName);
            }

            writer.WriteString("message", entry.Message);
            if (entry.CorrelationId is not null)
            {
                writer.WriteString("correlationId", entry.CorrelationId);
            }

            if (entry.Exception is not null)
            {
                writer.WriteString("exception", entry.Exception);
            }

            if (entry.Properties.Count > 0)
            {
                writer.WriteStartObject("props");
                foreach (var (key, value) in entry.Properties)
                {
                    writer.WriteString(key, value);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _queue.Writer.TryComplete();
        try
        {
            _writerTask.Wait(TimeSpan.FromSeconds(3));
        }
        catch (AggregateException)
        {
            // The writer loop never throws; ignore.
        }

        lock (_fileSync)
        {
            _stream?.Dispose();
            _stream = null;
        }
    }

    private async Task WriteLoopAsync()
    {
        var reader = _queue.Reader;
        while (await reader.WaitToReadAsync().ConfigureAwait(false))
        {
            lock (_fileSync)
            {
                while (reader.TryRead(out var entry))
                {
                    TryWrite(entry);
                }

                try
                {
                    _stream?.Flush(flushToDisk: false);
                }
                catch (IOException)
                {
                    // Disk full or removed; entries are kept in memory by the ring buffer.
                }
            }
        }
    }

    private void TryWrite(LogEntry entry)
    {
        try
        {
            var line = Serialize(entry);
            var stream = EnsureStream(entry.Timestamp, line.Length);
            stream.Write(line);
            stream.Write(s_newLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The file logger must never log through ILogger (recursion); report to the debug output only.
            System.Diagnostics.Debug.WriteLine($"Servora file logger failed: {ex.Message}");
            _stream?.Dispose();
            _stream = null;
        }
    }

    private FileStream EnsureStream(DateTimeOffset timestamp, int nextLength)
    {
        var day = timestamp.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var maxBytes = (long)_retention.MaxFileSizeMb * 1024 * 1024;
        if (_stream is not null && _currentDay == day && _stream.Length + nextLength <= maxBytes)
        {
            return _stream;
        }

        _stream?.Dispose();
        if (_currentDay != day)
        {
            _currentDay = day;
            _currentPart = 0;
        }

        while (true)
        {
            var path = Path.Combine(Directory, FileName(day, _currentPart));
            var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length + nextLength <= maxBytes || stream.Length == 0)
            {
                _stream = stream;
                return stream;
            }

            stream.Dispose();
            _currentPart++;
        }
    }

    private static string FileName(string day, int part) =>
        part == 0 ? $"{FilePrefix}{day}{FileExtension}" : string.Create(CultureInfo.InvariantCulture, $"{FilePrefix}{day}-{part:000}{FileExtension}");

    [GeneratedRegex(@"^servora-\d{8}(-\d{3})?\.jsonl$", RegexOptions.CultureInvariant)]
    private static partial Regex LogFileName();
}
