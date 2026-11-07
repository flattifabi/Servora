using Servora.Application.Cmdb.Graph;
using Servora.Application.Common;

namespace Servora.Application.Cmdb;

/// <summary>
/// CMDB use cases: configuration items, relationships (<c>cmdb_rel_ci</c>), the dependency graph and records related to a CI.
/// Reads require <c>cmdb.read</c>, changes <c>cmdb.write</c>, deletes <c>cmdb.manage</c>.
/// </summary>
public interface ICmdbService
{
    /// <summary>Gets the CMDB KPIs.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The overview.</returns>
    Task<Result<CmdbOverview>> GetOverviewAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists configuration items (paged).</summary>
    /// <param name="query">The filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page.</returns>
    Task<Result<PagedResult<CiSummary>>> ListAsync(CiQuery query, CancellationToken cancellationToken = default);

    /// <summary>Gets a configuration item.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The CI.</returns>
    Task<Result<CiDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a configuration item.</summary>
    /// <param name="input">The values.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifier.</returns>
    Task<Result<Guid>> CreateAsync(CiInput input, CancellationToken cancellationToken = default);

    /// <summary>Updates a configuration item.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="input">The values.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UpdateAsync(Guid id, CiInput input, CancellationToken cancellationToken = default);

    /// <summary>Deletes a configuration item and its relationships.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lists the relationships of a CI in both directions.</summary>
    /// <param name="id">The CI.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The relationships (max 500).</returns>
    Task<Result<IReadOnlyList<CiRelationshipDto>>> GetRelationshipsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Adds a relationship.</summary>
    /// <param name="input">The relationship.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifier.</returns>
    Task<Result<Guid>> AddRelationshipAsync(CiRelationshipInput input, CancellationToken cancellationToken = default);

    /// <summary>Removes a relationship.</summary>
    /// <param name="relationshipId">The relationship.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> RemoveRelationshipAsync(Guid relationshipId, CancellationToken cancellationToken = default);

    /// <summary>Builds the dependency graph around a CI.</summary>
    /// <param name="id">The focus CI.</param>
    /// <param name="depth">The depth (1–6).</param>
    /// <param name="direction">The direction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The graph.</returns>
    Task<Result<CiGraph>> GetGraphAsync(Guid id, int depth = 2, CiGraphDirection direction = CiGraphDirection.Both, CancellationToken cancellationToken = default);

    /// <summary>Gets records of other modules that reference the CI (respecting the caller's permissions).</summary>
    /// <param name="id">The CI.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The records.</returns>
    Task<Result<CiRelatedRecords>> GetRelatedRecordsAsync(Guid id, CancellationToken cancellationToken = default);
}
