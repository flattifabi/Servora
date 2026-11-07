using Servora.Application.Common;
using Servora.Application.Conditions;

namespace Servora.Application.Tables;

/// <summary>Generic query against any table.</summary>
public sealed record RecordQuery
{
    /// <summary>Gets the filter condition.</summary>
    public ConditionGroup? Condition { get; init; }

    /// <summary>Gets paging, sorting and free-text search.</summary>
    public PageRequest Page { get; init; } = new();

    /// <summary>Gets the fields to return; empty means all fields.</summary>
    public IReadOnlyList<string> Fields { get; init; } = [];

    /// <summary>Gets a value indicating whether reference fields should be resolved to display values (<c>field.display</c>).</summary>
    public bool IncludeDisplayValues { get; init; } = true;
}
