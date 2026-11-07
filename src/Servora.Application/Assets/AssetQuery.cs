using Servora.Application.Common;
using Servora.Domain.Assets;

namespace Servora.Application.Assets;

/// <summary>Filter for asset lists.</summary>
public sealed record AssetQuery
{
    /// <summary>Gets the state filter.</summary>
    public AssetState? State { get; init; }

    /// <summary>Gets the category filter.</summary>
    public AssetCategory? Category { get; init; }

    /// <summary>Gets a value indicating whether only active assets whose warranty ends within 90 days (or has ended) are returned.</summary>
    public bool WarrantyExpiringOnly { get; init; }

    /// <summary>Gets a value indicating whether only active (in stock, in use, in repair) assets are returned.</summary>
    public bool ActiveOnly { get; init; }

    /// <summary>Gets the assignee filter.</summary>
    public Guid? AssignedToId { get; init; }

    /// <summary>Gets paging, sorting (<c>Number</c>, <c>DisplayName</c>, <c>Category</c>, <c>State</c>, <c>WarrantyEnd</c>, <c>Location</c>) and search.</summary>
    public PageRequest Page { get; init; } = new();
}
