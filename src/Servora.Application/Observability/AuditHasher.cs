using System.Security.Cryptography;
using System.Text;
using Servora.Domain.Observability;

namespace Servora.Application.Observability;

/// <summary>
/// Canonical SHA-256 hash of an audit event (its content plus <see cref="AuditEvent.PreviousHash"/>). Shared by the
/// writer (<c>AuditService</c>) and the verifier (<see cref="AuditChainVerifier"/>) so both always agree.
/// </summary>
public static class AuditHasher
{
    /// <summary>Computes the chain hash of an event.</summary>
    /// <param name="auditEvent">The event (with <see cref="AuditEvent.PreviousHash"/> set).</param>
    /// <returns>The lower-case hex SHA-256 hash.</returns>
    public static string ComputeHash(AuditEvent auditEvent)
    {
        var canonical = string.Join('|',
            auditEvent.Id.ToString("N"),
            auditEvent.OccurredAt.ToUnixTimeMilliseconds(),
            auditEvent.Category,
            auditEvent.Action,
            (int)auditEvent.Outcome,
            auditEvent.ActorId,
            auditEvent.ActorName,
            auditEvent.ActorType,
            auditEvent.TargetType,
            auditEvent.TargetId,
            auditEvent.TargetName,
            auditEvent.Message,
            auditEvent.DetailsJson,
            auditEvent.IpAddress,
            auditEvent.CorrelationId,
            auditEvent.PreviousHash);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
