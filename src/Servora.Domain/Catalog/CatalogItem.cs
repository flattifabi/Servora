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

    /// <summary>Gets or sets the base price per unit (0 = no additional cost), billed according to <see cref="BillingType"/>.</summary>
    public decimal Price { get; set; }

    /// <summary>Gets or sets how the price is billed (one-time or monthly).</summary>
    public CatalogBillingType BillingType { get; set; } = CatalogBillingType.OneTime;

    /// <summary>Gets or sets the contract term in months of monthly billed items (<see langword="null"/> = no fixed term).</summary>
    public int? TermMonths { get; set; }

    /// <summary>Gets or sets the one-time costs per unit charged in addition to the monthly price (setup fee).</summary>
    public decimal OneTimePrice { get; set; }

    /// <summary>Gets or sets the subtitle shown below the name, for example the model ("Lenovo ThinkPad T14 Gen 5").</summary>
    public string? Subtitle { get; set; }

    /// <summary>Gets or sets a short badge text, for example "Our standard for you".</summary>
    public string? BadgeText { get; set; }

    /// <summary>Gets or sets the public service identifier shown on the order page, for example <c>HW-001</c>.</summary>
    public string? Code { get; set; }

    /// <summary>Gets or sets the identifier of the uploaded product image (a file in the data directory).</summary>
    public Guid? ImageId { get; set; }

    /// <summary>Gets or sets the headline of the card panel shown when the item has no image ("Better together.").</summary>
    public string? Teaser { get; set; }

    /// <summary>Gets or sets the line below the teaser headline ("Your digital toolkit").</summary>
    public string? TeaserText { get; set; }

    /// <summary>Gets or sets a delivery text that replaces the computed one, for example "Available immediately".</summary>
    public string? DeliveryText { get; set; }

    /// <summary>
    /// Gets or sets the highlight facts of the order page, one per line, optionally prefixed with an icon name
    /// (<c>truck | 3–5 working days</c>).
    /// </summary>
    public string? Highlights { get; set; }

    /// <summary>Gets or sets the "Details &amp; equipment" tab content (Markdown, rendered safely).</summary>
    public string? DetailsMarkdown { get; set; }

    /// <summary>Gets or sets the "Delivery &amp; support" tab content (Markdown, rendered safely).</summary>
    public string? DeliveryMarkdown { get; set; }

    /// <summary>Gets or sets a value indicating whether the item is featured on the portal home page.</summary>
    public bool IsFeatured { get; set; }

    /// <summary>Gets or sets the order among the featured items.</summary>
    public int FeaturedOrder { get; set; }

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
