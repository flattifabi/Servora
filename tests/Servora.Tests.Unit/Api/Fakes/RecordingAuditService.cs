using Servora.Application.Observability;

namespace Servora.Tests.Unit.Api.Fakes;

/// <summary><see cref="IAuditService"/> that records every entry in memory.</summary>
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
