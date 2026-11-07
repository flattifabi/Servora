using Servora.Domain.Common;

namespace Servora.Domain.Observability;

/// <summary>
/// Security and configuration audit record. Audit events are append-only and hash-chained
/// (<see cref="PreviousHash"/> / <see cref="Hash"/>) to make tampering detectable.
/// </summary>
public class AuditEvent : Entity
{
    /// <summary>Gets or sets the UTC time of the event.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Gets or sets the category, for example <c>auth</c>, <c>identity</c>, <c>config</c>, <c>apikey</c>, <c>mcp</c>.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Gets or sets the action, for example <c>login.success</c> or <c>role.updated</c>.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Gets or sets the outcome.</summary>
    public AuditOutcome Outcome { get; set; }

    /// <summary>Gets or sets the actor identifier (user id, API key id, agent id) if known.</summary>
    public string? ActorId { get; set; }

    /// <summary>Gets or sets the actor display name.</summary>
    public string ActorName { get; set; } = "System";

    /// <summary>Gets or sets the actor type: <c>user</c>, <c>apikey</c>, <c>agent</c>, <c>system</c>.</summary>
    public string ActorType { get; set; } = "system";

    /// <summary>Gets or sets the target type (table name or resource type).</summary>
    public string? TargetType { get; set; }

    /// <summary>Gets or sets the target identifier.</summary>
    public string? TargetId { get; set; }

    /// <summary>Gets or sets the target display value.</summary>
    public string? TargetName { get; set; }

    /// <summary>Gets or sets a short, secret-free, human readable message.</summary>
    public string? Message { get; set; }

    /// <summary>Gets or sets redacted JSON details (never secrets).</summary>
    public string? DetailsJson { get; set; }

    /// <summary>Gets or sets the client IP address.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Gets or sets the correlation identifier of the request / execution.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Gets or sets the hash of the previous audit event (chain link).</summary>
    public string? PreviousHash { get; set; }

    /// <summary>Gets or sets the SHA-256 hash of this event including <see cref="PreviousHash"/>.</summary>
    public string? Hash { get; set; }
}
