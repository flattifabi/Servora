using Servora.Domain.Common;

namespace Servora.Domain.Assets;

/// <summary>
/// A hardware, software or license asset (<c>alm_asset</c>) tracked through its financial and physical lifecycle.
/// License assets additionally track entitlements versus installations.
/// </summary>
[RecordTable(TableNames.Asset, "Asset", PluralLabel = "Assets", DisplayField = nameof(Number), Icon = "box", Route = "/assets")]
public class Asset : AuditableEntity, IHasCustomFields
{
    /// <summary>Gets or sets the human readable number, for example <c>AST0001001</c>.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name, for example "MacBook Pro 14 – J. Smith".</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets or sets the category.</summary>
    public AssetCategory Category { get; set; } = AssetCategory.Hardware;

    /// <summary>Gets or sets the model / product name.</summary>
    public string? Model { get; set; }

    /// <summary>Gets or sets the manufacturer / publisher.</summary>
    public string? Manufacturer { get; set; }

    /// <summary>Gets or sets the vendor the asset was purchased from.</summary>
    public string? Vendor { get; set; }

    /// <summary>Gets or sets the serial number.</summary>
    public string? SerialNumber { get; set; }

    /// <summary>Gets or sets the asset tag (physical label).</summary>
    public string? AssetTag { get; set; }

    /// <summary>Gets or sets the lifecycle state.</summary>
    public AssetState State { get; set; } = AssetState.InStock;

    /// <summary>Gets or sets the user the asset is assigned to.</summary>
    [Reference(TableNames.User)]
    public Guid? AssignedToId { get; set; }

    /// <summary>Gets or sets the location.</summary>
    public string? Location { get; set; }

    /// <summary>Gets or sets the purchase cost.</summary>
    public decimal Cost { get; set; }

    /// <summary>Gets or sets the ISO currency code of <see cref="Cost"/>.</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Gets or sets the purchase date.</summary>
    public DateOnly? PurchaseDate { get; set; }

    /// <summary>Gets or sets the end of the warranty (or the license expiry for licenses).</summary>
    public DateOnly? WarrantyEnd { get; set; }

    /// <summary>Gets or sets the linked configuration item.</summary>
    [Reference(TableNames.ConfigurationItem)]
    public Guid? ConfigurationItemId { get; set; }

    /// <summary>Gets or sets the number of purchased entitlements (licenses only).</summary>
    public int? Entitlements { get; set; }

    /// <summary>Gets or sets the number of installations / assigned seats (licenses only).</summary>
    public int? Installs { get; set; }

    /// <summary>Gets or sets free-form notes.</summary>
    public string? Notes { get; set; }

    /// <summary>Gets or sets the UTC time the asset was retired.</summary>
    public DateTimeOffset? RetiredAt { get; set; }

    /// <inheritdoc />
    public string? CustomFieldsJson { get; set; }
}
