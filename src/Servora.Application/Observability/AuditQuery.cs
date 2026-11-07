using Servora.Domain.Observability;

namespace Servora.Application.Observability;

/// <summary>Filter for the audit log viewer and CSV export. All criteria are optional.</summary>
public sealed record AuditQuery
{
    /// <summary>Gets the category (exact match).</summary>
    public string? Category { get; init; }

    /// <summary>Gets an action filter (prefix match, for example <c>login.</c>).</summary>
    public string? Action { get; init; }

    /// <summary>Gets an actor filter (substring of the actor name).</summary>
    public string? Actor { get; init; }

    /// <summary>Gets the outcome.</summary>
    public AuditOutcome? Outcome { get; init; }

    /// <summary>Gets the earliest time (inclusive).</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Gets the latest time (exclusive).</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>Gets a free-text filter over message, target and action.</summary>
    public string? Search { get; init; }
}
