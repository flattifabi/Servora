using Servora.Application.Common;
using Servora.Domain.Automation;

namespace Servora.Application.Automation.Services;

/// <summary>Filter of the flow list.</summary>
public sealed record FlowListQuery
{
    /// <summary>Gets paging, sorting (<c>Name</c>, <c>UpdatedAt</c>, <c>Trigger</c>) and search.</summary>
    public PageRequest Page { get; init; } = new();

    /// <summary>Gets the active filter (<see langword="null"/> = all).</summary>
    public bool? Active { get; init; }

    /// <summary>Gets the trigger filter.</summary>
    public FlowTriggerType? Trigger { get; init; }
}
