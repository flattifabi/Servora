using System.Collections.Concurrent;
using Servora.Application.Observability;

namespace Servora.Tests.Unit.Automation.Fakes;

/// <summary><see cref="IAuditService"/> that keeps entries in memory.</summary>
internal sealed class RecordingAuditService : IAuditService
{
    /// <summary>Gets the written entries.</summary>
    public ConcurrentQueue<AuditEntry> Entries { get; } = new();

    /// <inheritdoc />
    public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        Entries.Enqueue(entry);
        return Task.CompletedTask;
    }
}
