namespace Servora.Domain.Assets;

/// <summary>Pure lifecycle rules of assets: In stock ⇄ In use ⇄ In repair → Retired → Disposed.</summary>
public static class AssetLifecycle
{
    /// <summary>Gets the ordered states for state flow visualisations.</summary>
    public static IReadOnlyList<AssetState> OrderedStates { get; } =
        [AssetState.InStock, AssetState.InUse, AssetState.InRepair, AssetState.Retired, AssetState.Disposed];

    /// <summary>Gets a value indicating whether an asset may move from one state to another.</summary>
    /// <param name="from">The current state.</param>
    /// <param name="to">The target state.</param>
    /// <returns><see langword="true"/> when the transition is allowed.</returns>
    public static bool CanTransition(AssetState from, AssetState to) => from != to && AllowedTargets(from).Contains(to);

    /// <summary>Gets the states reachable from a state.</summary>
    /// <param name="from">The current state.</param>
    /// <returns>The allowed target states.</returns>
    public static IReadOnlyList<AssetState> AllowedTargets(AssetState from) => from switch
    {
        AssetState.InStock => [AssetState.InUse, AssetState.InRepair, AssetState.Retired],
        AssetState.InUse => [AssetState.InStock, AssetState.InRepair, AssetState.Retired],
        AssetState.InRepair => [AssetState.InStock, AssetState.InUse, AssetState.Retired],
        AssetState.Retired => [AssetState.InStock, AssetState.Disposed],
        _ => [],
    };

    /// <summary>Gets a value indicating whether an asset in the given state counts as active inventory.</summary>
    /// <param name="state">The state.</param>
    /// <returns><see langword="true"/> for in stock, in use and in repair.</returns>
    public static bool IsActive(AssetState state) => state is AssetState.InStock or AssetState.InUse or AssetState.InRepair;

    /// <summary>Gets the display label of a state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The label.</returns>
    public static string Label(AssetState state) => state switch
    {
        AssetState.InStock => "In stock",
        AssetState.InUse => "In use",
        AssetState.InRepair => "In repair",
        AssetState.Retired => "Retired",
        AssetState.Disposed => "Disposed",
        _ => state.ToString(),
    };
}
