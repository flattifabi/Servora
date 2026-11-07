using Servora.Domain.Observability;

namespace Servora.Application.Observability;

/// <summary>Input for <see cref="IAuditService.WriteAsync"/>.</summary>
/// <param name="Category">The category, for example <c>auth</c>, <c>identity</c>, <c>config</c>.</param>
/// <param name="Action">The action, for example <c>login.failed</c>.</param>
/// <param name="Outcome">The outcome.</param>
public sealed record AuditEntry(string Category, string Action, AuditOutcome Outcome = AuditOutcome.Success)
{
    /// <summary>Gets the target type (table or resource type).</summary>
    public string? TargetType { get; init; }

    /// <summary>Gets the target identifier.</summary>
    public string? TargetId { get; init; }

    /// <summary>Gets the target display value.</summary>
    public string? TargetName { get; init; }

    /// <summary>Gets the user-safe message.</summary>
    public string? Message { get; init; }

    /// <summary>Gets optional secret-free details that are serialized to JSON.</summary>
    public object? Details { get; init; }

    /// <summary>Gets an explicit actor name override (for example the attempted user name of a failed login).</summary>
    public string? ActorNameOverride { get; init; }
}
