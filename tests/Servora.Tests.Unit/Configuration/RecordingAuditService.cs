using Servora.Application.Observability;

namespace Servora.Tests.Unit.Configuration;

/// <summary><see cref="IAuditService"/> that records entries in memory.</summary>
internal sealed class RecordingAuditService : IAuditService
{
    /// <summary>Gets the written entries.</summary>
    public List<AuditEntry> Entries { get; } = [];

    /// <inheritdoc />
    public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }
}
