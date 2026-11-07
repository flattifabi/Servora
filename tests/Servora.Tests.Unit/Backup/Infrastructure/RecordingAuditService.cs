using Servora.Application.Observability;

namespace Servora.Tests.Unit.Backup.Infrastructure;

/// <summary><see cref="IAuditService"/> that records the written entries.</summary>
internal sealed class RecordingAuditService : IAuditService
{
    /// <summary>Gets the written entries.</summary>
    public List<AuditEntry> Entries { get; } = [];

    /// <inheritdoc />
    public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        lock (Entries)
        {
            Entries.Add(entry);
        }

        return Task.CompletedTask;
    }
}
