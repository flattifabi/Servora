namespace Servora.Domain.Assets;

/// <summary>Lifecycle state of an asset (see <see cref="AssetLifecycle"/>).</summary>
public enum AssetState
{
    /// <summary>Received and in stock, not assigned.</summary>
    InStock = 1,

    /// <summary>In use (deployed / assigned).</summary>
    InUse = 2,

    /// <summary>Being repaired.</summary>
    InRepair = 3,

    /// <summary>Retired from use, awaiting disposal.</summary>
    Retired = 4,

    /// <summary>Disposed (final).</summary>
    Disposed = 5,
}
