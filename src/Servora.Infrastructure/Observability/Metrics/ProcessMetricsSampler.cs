using System.Diagnostics;
using Servora.Application.Common;
using Servora.Application.Observability;

namespace Servora.Infrastructure.Observability.Metrics;

/// <summary>
/// <see cref="IProcessMetrics"/> based on <see cref="Process"/>: CPU usage is computed from the processor time delta
/// between two samples. Keeps the last <see cref="HistorySize"/> samples (2 hours at the default 30 s interval).
/// </summary>
public sealed class ProcessMetricsSampler : IProcessMetrics
{
    /// <summary>Number of samples kept.</summary>
    public const int HistorySize = 240;

    private readonly IClock _clock;
    private readonly object _sync = new();
    private readonly Queue<ProcessSample> _history = new();
    private TimeSpan _lastCpu;
    private DateTimeOffset _lastAt;
    private ProcessSample? _current;

    /// <summary>Initializes a new instance of the <see cref="ProcessMetricsSampler"/> class.</summary>
    /// <param name="clock">The clock.</param>
    public ProcessMetricsSampler(IClock clock)
    {
        _clock = clock;
        using var process = Process.GetCurrentProcess();
        StartedAt = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
        _lastCpu = process.TotalProcessorTime;
        _lastAt = clock.UtcNow;
    }

    /// <inheritdoc />
    public DateTimeOffset StartedAt { get; }

    /// <inheritdoc />
    public ProcessSample Current
    {
        get
        {
            lock (_sync)
            {
                if (_current is not null)
                {
                    return _current;
                }
            }

            return Sample();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ProcessSample> History
    {
        get
        {
            lock (_sync)
            {
                return _history.ToList();
            }
        }
    }

    /// <inheritdoc />
    public ProcessSample Sample()
    {
        using var process = Process.GetCurrentProcess();
        var now = _clock.UtcNow;
        lock (_sync)
        {
            var cpu = process.TotalProcessorTime;
            var wall = (now - _lastAt).TotalMilliseconds;
            var percent = wall <= 0 ? _current?.CpuPercent ?? 0 : (cpu - _lastCpu).TotalMilliseconds / (wall * Environment.ProcessorCount) * 100;
            _lastCpu = cpu;
            _lastAt = now;

            var sample = new ProcessSample(now, Math.Round(Math.Clamp(percent, 0, 100), 1), process.WorkingSet64, GC.GetTotalMemory(false), process.Threads.Count);
            _current = sample;
            _history.Enqueue(sample);
            while (_history.Count > HistorySize)
            {
                _history.Dequeue();
            }

            return sample;
        }
    }
}
