using Servora.Application.Observability;

namespace Servora.Tests.Unit.Portal;

/// <summary>Audit service that records entries in memory.</summary>
public sealed class PortalTestAudit : IAuditService
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
