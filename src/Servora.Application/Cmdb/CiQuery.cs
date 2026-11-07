using Servora.Application.Common;
using Servora.Domain.Cmdb;

namespace Servora.Application.Cmdb;

/// <summary>Filter for configuration item lists.</summary>
public sealed record CiQuery
{
    /// <summary>Gets the class filter (includes sub-classes).</summary>
    public string? ClassKey { get; init; }

    /// <summary>Gets the operational status filter.</summary>
    public OperationalStatus? Status { get; init; }

    /// <summary>Gets the environment filter.</summary>
    public string? Environment { get; init; }

    /// <summary>Gets paging, sorting (<c>Name</c>, <c>ClassName</c>, <c>Status</c>, <c>Environment</c>, <c>Criticality</c>, <c>UpdatedAt</c>) and search.</summary>
    public PageRequest Page { get; init; } = new();
}
