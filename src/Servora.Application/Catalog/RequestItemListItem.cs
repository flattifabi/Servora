using Servora.Domain.Approvals;
using Servora.Domain.Catalog;

namespace Servora.Application.Catalog;

/// <summary>A row of the requested item list.</summary>
public sealed record RequestItemListItem
{
    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the number.</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>Gets the parent request identifier.</summary>
    public Guid RequestId { get; init; }

    /// <summary>Gets the parent request number.</summary>
    public string? RequestNumber { get; init; }

    /// <summary>Gets the item name.</summary>
    public string ItemName { get; init; } = string.Empty;

    /// <summary>Gets the quantity.</summary>
    public int Quantity { get; init; }

    /// <summary>Gets the requested-for user name.</summary>
    public string? RequestedForName { get; init; }

    /// <summary>Gets the state.</summary>
    public RequestItemState State { get; init; }

    /// <summary>Gets the approval status.</summary>
    public ApprovalStatus Approval { get; init; }

    /// <summary>Gets the fulfilment group name.</summary>
    public string? AssignmentGroupName { get; init; }

    /// <summary>Gets the opening time.</summary>
    public DateTimeOffset OpenedAt { get; init; }

    /// <summary>Gets the due date.</summary>
    public DateTimeOffset? DueAt { get; init; }
}
