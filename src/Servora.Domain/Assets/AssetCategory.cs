namespace Servora.Domain.Assets;

/// <summary>Category of an asset.</summary>
public enum AssetCategory
{
    /// <summary>Physical hardware (laptops, servers, network gear, phones).</summary>
    Hardware = 1,

    /// <summary>Installed software.</summary>
    Software = 2,

    /// <summary>Software license / entitlement.</summary>
    License = 3,
}
