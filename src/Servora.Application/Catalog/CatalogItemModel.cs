using Servora.Domain.Approvals;

namespace Servora.Application.Catalog;

/// <summary>Editable fields of a catalog item (catalog builder) plus its variables (read only, edited individually).</summary>
public sealed class CatalogItemModel
{
    /// <summary>Gets or sets the identifier (<see langword="null"/> for new items).</summary>
    public Guid? Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the short description.</summary>
    public string? ShortDescription { get; set; }

    /// <summary>Gets or sets the long description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the icon name.</summary>
    public string Icon { get; set; } = "package";

    /// <summary>Gets or sets the category.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Gets or sets the price.</summary>
    public decimal Price { get; set; }

    /// <summary>Gets or sets the delivery time in days.</summary>
    public int DeliveryDays { get; set; } = 3;

    /// <summary>Gets or sets the fulfilment group.</summary>
    public Guid? FulfillmentGroupId { get; set; }

    /// <summary>Gets or sets the fulfilment task names, one per line.</summary>
    public string? FulfillmentTasks { get; set; }

    /// <summary>Gets or sets a value indicating whether approval is required.</summary>
    public bool ApprovalRequired { get; set; }

    /// <summary>Gets or sets the approval group.</summary>
    public Guid? ApprovalGroupId { get; set; }

    /// <summary>Gets or sets a value indicating whether the requested-for user's manager must approve.</summary>
    public bool ManagerApproval { get; set; }

    /// <summary>Gets or sets how approver decisions are combined.</summary>
    public ApprovalMode ApprovalMode { get; set; } = ApprovalMode.AnyApprover;

    /// <summary>Gets or sets the sort order.</summary>
    public int Order { get; set; }

    /// <summary>Gets or sets a value indicating whether the item is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Gets or sets the optimistic concurrency version.</summary>
    public long Version { get; set; }

    /// <summary>Gets or sets the variables in display order (read only in this model).</summary>
    public List<CatalogVariableDefinition> Variables { get; set; } = [];
}
