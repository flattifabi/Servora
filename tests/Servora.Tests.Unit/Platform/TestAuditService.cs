using System.Collections.Concurrent;
using Servora.Application.Observability;

namespace Servora.Tests.Unit.Platform;

/// <summary>Captures audit entries.</summary>
public sealed class TestAuditService : IAuditService
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
