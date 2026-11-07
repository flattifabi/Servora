using Servora.Application.Common;
using Servora.Domain.Assets;

namespace Servora.Application.Assets;

/// <summary>
/// Asset management use cases: inventory, lifecycle transitions, licenses and KPIs. Reads require <c>asset.read</c>,
/// changes <c>asset.write</c>, deletes <c>asset.manage</c>.
/// </summary>
public interface IAssetService
{
    /// <summary>Gets the asset KPIs.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The overview.</returns>
    Task<Result<AssetOverview>> GetOverviewAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists assets (paged).</summary>
    /// <param name="query">The filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page.</returns>
    Task<Result<PagedResult<AssetSummary>>> ListAsync(AssetQuery query, CancellationToken cancellationToken = default);

    /// <summary>Gets an asset.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The asset.</returns>
    Task<Result<AssetDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates an asset.</summary>
    /// <param name="input">The values.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifier.</returns>
    Task<Result<Guid>> CreateAsync(AssetInput input, CancellationToken cancellationToken = default);

    /// <summary>Updates an asset (not its state).</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="input">The values.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UpdateAsync(Guid id, AssetInput input, CancellationToken cancellationToken = default);

    /// <summary>Moves an asset to another lifecycle state (see <see cref="AssetLifecycle"/>).</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="target">The target state.</param>
    /// <param name="note">An optional note written to the activity stream.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> ChangeStateAsync(Guid id, AssetState target, string? note = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes an asset.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
