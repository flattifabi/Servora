using Servora.Domain.Approvals;
using Servora.Domain.Common;

namespace Servora.Domain.Catalog;

/// <summary>An orderable product or service of the service catalog (for example "New laptop").</summary>
[RecordTable(CatalogTables.Item, "Catalog Item", PluralLabel = "Catalog Items", DisplayField = nameof(Name), Icon = "package", Route = "/catalog-admin")]
public class CatalogItem : AuditableEntity
{
    /// <summary>Gets or sets the item name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the one-line description shown on catalog cards.</summary>
    public string? ShortDescription { get; set; }

    /// <summary>Gets or sets the long description shown on the order form.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the icon name (see the UI icon set).</summary>
    public string Icon { get; set; } = "package";

    /// <summary>Gets or sets the category.</summary>
    [Reference(CatalogTables.Category)]
    public Guid? CategoryId { get; set; }

    /// <summary>Gets or sets the one-time price per unit (0 = free).</summary>
    public decimal Price { get; set; }

    /// <summary>Gets or sets the expected delivery time in days.</summary>
    public int DeliveryDays { get; set; } = 3;

    /// <summary>Gets or sets the group that fulfils the item.</summary>
    [Reference(TableNames.Group)]
    public Guid? FulfillmentGroupId { get; set; }

    /// <summary>
    /// Gets or sets the fulfilment task names, one per line. One catalog task is created per line when the item is
    /// approved; when empty a single task is created.
    /// </summary>
    public string? FulfillmentTasks { get; set; }

    /// <summary>Gets or sets a value indicating whether an approval is required before fulfilment.</summary>
    public bool ApprovalRequired { get; set; }

    /// <summary>Gets or sets the approval group.</summary>
    [Reference(TableNames.Group)]
    public Guid? ApprovalGroupId { get; set; }

    /// <summary>Gets or sets a value indicating whether the manager of the requested-for user must approve.</summary>
    public bool ManagerApproval { get; set; }

    /// <summary>Gets or sets how the approver decisions are combined.</summary>
    public ApprovalMode ApprovalMode { get; set; } = ApprovalMode.AnyApprover;

    /// <summary>Gets or sets the sort order within the category.</summary>
    public int Order { get; set; }

    /// <summary>Gets or sets a value indicating whether the item can be ordered.</summary>
    public bool IsActive { get; set; } = true;
}
