using Servora.Domain.Approvals;
using Servora.Domain.Catalog;

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

    /// <summary>Gets or sets the order form sections in display order (read only in this model, edited individually).</summary>
    public List<CatalogSectionModel> Sections { get; set; } = [];

    /// <summary>Gets or sets how the price is billed.</summary>
    public CatalogBillingType BillingType { get; set; } = CatalogBillingType.OneTime;

    /// <summary>Gets or sets the contract term in months (monthly billed items).</summary>
    public int? TermMonths { get; set; }

    /// <summary>Gets or sets the one-time costs per unit (monthly billed items).</summary>
    public decimal OneTimePrice { get; set; }

    /// <summary>Gets or sets the subtitle (for example the model).</summary>
    public string? Subtitle { get; set; }

    /// <summary>Gets or sets the badge text.</summary>
    public string? BadgeText { get; set; }

    /// <summary>Gets or sets the public service identifier (letters, digits, dashes).</summary>
    public string? Code { get; set; }

    /// <summary>Gets or sets the product image identifier (read only, changed through the image upload).</summary>
    public Guid? ImageId { get; set; }

    /// <summary>Gets the relative URL of the product image.</summary>
    public string? ImageUrl => ICatalogImageStore.UrlOf(ImageId);

    /// <summary>Gets or sets the teaser headline shown on cards without an image.</summary>
    public string? Teaser { get; set; }

    /// <summary>Gets or sets the line below the teaser headline.</summary>
    public string? TeaserText { get; set; }

    /// <summary>Gets or sets the delivery text that replaces the computed one.</summary>
    public string? DeliveryText { get; set; }

    /// <summary>Gets or sets the highlight lines (<c>icon | text</c>).</summary>
    public string? Highlights { get; set; }

    /// <summary>Gets or sets the "Details &amp; equipment" Markdown.</summary>
    public string? DetailsMarkdown { get; set; }

    /// <summary>Gets or sets the "Delivery &amp; support" Markdown.</summary>
    public string? DeliveryMarkdown { get; set; }

    /// <summary>Gets or sets a value indicating whether the item is featured on the portal home page.</summary>
    public bool IsFeatured { get; set; }

    /// <summary>Gets or sets the order among featured items.</summary>
    public int FeaturedOrder { get; set; }
}
