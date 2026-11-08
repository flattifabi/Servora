using Servora.Domain.Approvals;
using Servora.Domain.Catalog;

namespace Servora.Application.Catalog;

/// <summary>A requested item with its answers and tasks (request record page).</summary>
public sealed record RequestItemDetail
{
    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the number.</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>Gets the catalog item identifier.</summary>
    public Guid CatalogItemId { get; init; }

    /// <summary>Gets the item name.</summary>
    public string ItemName { get; init; } = string.Empty;

    /// <summary>Gets the item icon.</summary>
    public string Icon { get; init; } = "package";

    /// <summary>Gets the quantity.</summary>
    public int Quantity { get; init; }

    /// <summary>Gets the unit price (monthly price for monthly billed items).</summary>
    public decimal UnitPrice { get; init; }

    /// <summary>Gets how the unit price is billed.</summary>
    public CatalogBillingType BillingType { get; init; }

    /// <summary>Gets the contract term in months of monthly billed items.</summary>
    public int? TermMonths { get; init; }

    /// <summary>Gets the state.</summary>
    public RequestItemState State { get; init; }

    /// <summary>Gets the approval status.</summary>
    public ApprovalStatus Approval { get; init; }

    /// <summary>Gets the fulfilment group name.</summary>
    public string? AssignmentGroupName { get; init; }

    /// <summary>Gets the opening time.</summary>
    public DateTimeOffset OpenedAt { get; init; }

    /// <summary>Gets the closing time.</summary>
    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary>Gets the answers in form order.</summary>
    public IReadOnlyList<VariableAnswer> Answers { get; init; } = [];

    /// <summary>Gets the catalog tasks.</summary>
    public IReadOnlyList<CatalogTaskListItem> Tasks { get; init; } = [];
}
