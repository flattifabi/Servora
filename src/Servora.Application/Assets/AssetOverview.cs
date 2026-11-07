using Servora.Domain.Assets;

namespace Servora.Application.Assets;

/// <summary>Asset management KPIs.</summary>
public sealed class AssetOverview
{
    /// <summary>Gets or sets the total number of assets.</summary>
    public int Total { get; set; }

    /// <summary>Gets or sets the number of assets per state.</summary>
    public IReadOnlyDictionary<AssetState, int> ByState { get; set; } = new Dictionary<AssetState, int>();

    /// <summary>Gets or sets the number of assets per category.</summary>
    public IReadOnlyDictionary<AssetCategory, int> ByCategory { get; set; } = new Dictionary<AssetCategory, int>();

    /// <summary>Gets or sets the purchase value of active assets per currency.</summary>
    public IReadOnlyDictionary<string, decimal> ActiveValue { get; set; } = new Dictionary<string, decimal>();

    /// <summary>Gets or sets the number of active assets whose warranty ends within 90 days.</summary>
    public int WarrantyExpiringSoon { get; set; }

    /// <summary>Gets or sets the number of active assets whose warranty has ended.</summary>
    public int WarrantyExpired { get; set; }

    /// <summary>Gets or sets the total license entitlements of active license assets.</summary>
    public int LicenseEntitlements { get; set; }

    /// <summary>Gets or sets the total license installations of active license assets.</summary>
    public int LicenseInstalls { get; set; }

    /// <summary>Gets or sets the number of license assets with more installations than entitlements.</summary>
    public int OverDeployedLicenses { get; set; }

    /// <summary>Gets or sets the number of purchases per month for the last 12 months (oldest first).</summary>
    public IReadOnlyList<(string Month, int Count)> PurchasesPerMonth { get; set; } = [];
}
