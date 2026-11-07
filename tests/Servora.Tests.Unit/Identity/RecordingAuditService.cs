using Servora.Application.Observability;

namespace Servora.Tests.Unit.Identity;

/// <summary>Audit service that records entries in memory for assertions.</summary>
public sealed class RecordingAuditService : IAuditService
{
    /// <summary>Gets the recorded entries.</summary>
    public List<AuditEntry> Entries { get; } = [];

    /// <inheritdoc />
    public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }
}
