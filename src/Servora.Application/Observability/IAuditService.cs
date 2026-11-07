using Servora.Domain.Observability;

namespace Servora.Application.Observability;

/// <summary>
/// Writes security and configuration audit events. Implementations fill actor, IP and correlation information from
/// <see cref="Common.ICurrentUser"/> and must never persist secrets.
/// </summary>
public interface IAuditService
{
    /// <summary>Writes an audit event.</summary>
    /// <param name="entry">The event to write.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the event is persisted.</returns>
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
