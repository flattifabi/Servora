using Servora.Domain.Catalog;

namespace Servora.Application.Catalog;

/// <summary>A request with its requested items and tasks (request record page, portal request view).</summary>
public sealed record RequestDetail
{
    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the number.</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>Gets the short description.</summary>
    public string ShortDescription { get; init; } = string.Empty;

    /// <summary>Gets the state.</summary>
    public RequestState State { get; init; }

    /// <summary>Gets the requested-for user.</summary>
    public Guid? RequestedForId { get; init; }

    /// <summary>Gets the requested-for user name.</summary>
    public string? RequestedForName { get; init; }

    /// <summary>Gets the name of the user that placed the order.</summary>
    public string? OpenedByName { get; init; }

    /// <summary>Gets the opening time.</summary>
    public DateTimeOffset OpenedAt { get; init; }

    /// <summary>Gets the expected delivery date.</summary>
    public DateTimeOffset? DueAt { get; init; }

    /// <summary>Gets the closing time.</summary>
    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary>Gets the total price.</summary>
    public decimal TotalPrice { get; init; }

    /// <summary>Gets the requested items.</summary>
    public IReadOnlyList<RequestItemDetail> Items { get; init; } = [];

    /// <summary>Gets a value indicating whether the current user can work on the request (fulfiller).</summary>
    public bool CanFulfil { get; init; }

    /// <summary>Gets a value indicating whether the current user can cancel the request.</summary>
    public bool CanCancel { get; init; }
}
