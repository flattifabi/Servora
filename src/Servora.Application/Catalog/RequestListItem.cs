using Servora.Domain.Catalog;

namespace Servora.Application.Catalog;

/// <summary>A row of the request list.</summary>
public sealed record RequestListItem
{
    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the number.</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>Gets the short description.</summary>
    public string ShortDescription { get; init; } = string.Empty;

    /// <summary>Gets the requested-for user name.</summary>
    public string? RequestedForName { get; init; }

    /// <summary>Gets the state.</summary>
    public RequestState State { get; init; }

    /// <summary>Gets the number of requested items.</summary>
    public int ItemCount { get; init; }

    /// <summary>Gets the total price.</summary>
    public decimal TotalPrice { get; init; }

    /// <summary>Gets the opening time.</summary>
    public DateTimeOffset OpenedAt { get; init; }

    /// <summary>Gets the expected delivery date.</summary>
    public DateTimeOffset? DueAt { get; init; }
}
