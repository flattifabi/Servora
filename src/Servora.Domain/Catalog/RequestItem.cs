using Servora.Domain.Approvals;
using Servora.Domain.Common;
using Servora.Domain.Itsm;

namespace Servora.Domain.Catalog;

/// <summary>One ordered catalog item of a <see cref="Request"/> including the answers to the item's variables.</summary>
[RecordTable(TableNames.RequestItem, "Requested Item", PluralLabel = "Requested Items", Icon = "package", Route = "/requests")]
public class RequestItem : TaskRecord
{
    /// <summary>Gets or sets the parent request.</summary>
    [Reference(TableNames.Request)]
    public Guid RequestId { get; set; }

    /// <summary>Gets or sets the ordered catalog item.</summary>
    [Reference(CatalogTables.Item)]
    public Guid CatalogItemId { get; set; }

    /// <summary>Gets or sets the user the item is for.</summary>
    [Reference(TableNames.User)]
    public Guid? RequestedForId { get; set; }

    /// <summary>Gets or sets the ordered quantity.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Gets or sets the unit price at the time of ordering.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Gets or sets the validated variable answers as a JSON object keyed by variable name.</summary>
    public string? VariablesJson { get; set; }

    /// <summary>Gets or sets the item state.</summary>
    public RequestItemState State { get; set; } = RequestItemState.PendingApproval;

    /// <summary>Gets or sets the aggregated approval status.</summary>
    public ApprovalStatus Approval { get; set; } = ApprovalStatus.NotRequested;
}
