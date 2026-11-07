using Servora.Application.Common;
using Servora.Domain.Automation;

namespace Servora.Application.Automation.Services;

/// <summary>Filter of the execution list.</summary>
public sealed record FlowRunQuery
{
    /// <summary>Gets paging, sorting (<c>QueuedAt</c>, <c>Flow</c>, <c>Status</c>) and search.</summary>
    public PageRequest Page { get; init; } = new() { SortBy = "QueuedAt", SortDescending = true };

    /// <summary>Gets the status filter.</summary>
    public FlowRunStatus? Status { get; init; }

    /// <summary>Gets the flow filter.</summary>
    public Guid? FlowId { get; init; }

    /// <summary>Gets a value indicating whether test runs are included.</summary>
    public bool IncludeTests { get; init; } = true;
}
