namespace Servora.Application.Common;

/// <summary>Bounded paging, sorting and search parameters for list queries.</summary>
public sealed record PageRequest
{
    /// <summary>The maximum page size accepted by any list operation.</summary>
    public const int MaxPageSize = 500;

    private readonly int _page = 1;
    private readonly int _pageSize = 25;

    /// <summary>Gets the 1-based page number.</summary>
    public int Page { get => _page; init => _page = Math.Max(1, value); }

    /// <summary>Gets the page size, clamped to 1..<see cref="MaxPageSize"/>.</summary>
    public int PageSize { get => _pageSize; init => _pageSize = Math.Clamp(value, 1, MaxPageSize); }

    /// <summary>Gets the free-text search term.</summary>
    public string? Search { get; init; }

    /// <summary>Gets the field to sort by.</summary>
    public string? SortBy { get; init; }

    /// <summary>Gets a value indicating whether to sort descending.</summary>
    public bool SortDescending { get; init; }

    /// <summary>Gets the number of items to skip.</summary>
    public int Skip => (Page - 1) * PageSize;
}
