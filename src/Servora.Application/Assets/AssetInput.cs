using Servora.Domain.Assets;

namespace Servora.Application.Assets;

/// <summary>Editable fields of an asset (the state is changed through lifecycle transitions).</summary>
public sealed class AssetInput
{
    /// <summary>Gets or sets the display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets or sets the category.</summary>
    public AssetCategory Category { get; set; } = AssetCategory.Hardware;

    /// <summary>Gets or sets the model.</summary>
    public string? Model { get; set; }

    /// <summary>Gets or sets the manufacturer.</summary>
    public string? Manufacturer { get; set; }

    /// <summary>Gets or sets the vendor.</summary>
    public string? Vendor { get; set; }

    /// <summary>Gets or sets the serial number.</summary>
    public string? SerialNumber { get; set; }

    /// <summary>Gets or sets the asset tag.</summary>
    public string? AssetTag { get; set; }

    /// <summary>Gets or sets the initial state (only used when creating).</summary>
    public AssetState State { get; set; } = AssetState.InStock;

    /// <summary>Gets or sets the assignee.</summary>
    public Guid? AssignedToId { get; set; }

    /// <summary>Gets or sets the location.</summary>
    public string? Location { get; set; }

    /// <summary>Gets or sets the purchase cost.</summary>
    public decimal Cost { get; set; }

    /// <summary>Gets or sets the ISO currency code.</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Gets or sets the purchase date.</summary>
    public DateOnly? PurchaseDate { get; set; }

    /// <summary>Gets or sets the warranty / license end date.</summary>
    public DateOnly? WarrantyEnd { get; set; }

    /// <summary>Gets or sets the linked configuration item.</summary>
    public Guid? ConfigurationItemId { get; set; }

    /// <summary>Gets or sets the license entitlements.</summary>
    public int? Entitlements { get; set; }

    /// <summary>Gets or sets the license installations.</summary>
    public int? Installs { get; set; }

    /// <summary>Gets or sets notes.</summary>
    public string? Notes { get; set; }

    /// <summary>Gets or sets the expected concurrency version (0 for new assets).</summary>
    public long Version { get; set; }
}
