using System.Collections.Concurrent;
using Servora.Application.Common;
using Servora.Application.Observability;

namespace Servora.Infrastructure.Observability.Metrics;

/// <summary>Thread-safe <see cref="IHeartbeatRegistry"/> (at most 100 distinct services).</summary>
/// <param name="clock">The clock.</param>
public sealed class HeartbeatRegistry(IClock clock) : IHeartbeatRegistry
{
    private readonly ConcurrentDictionary<string, HeartbeatInfo> _beats = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Beat(string name, TimeSpan expectedInterval, string? status = null)
    {
        if (string.IsNullOrWhiteSpace(name) || (_beats.Count >= 100 && !_beats.ContainsKey(name)))
        {
            return;
        }

        var trimmed = name.Length > 100 ? name[..100] : name;
        _beats[trimmed] = new HeartbeatInfo(trimmed, clock.UtcNow, expectedInterval, status is { Length: > 200 } ? status[..200] : status);
    }

    /// <inheritdoc />
    public IReadOnlyList<HeartbeatInfo> GetAll() => _beats.Values.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase).ToList();
}
