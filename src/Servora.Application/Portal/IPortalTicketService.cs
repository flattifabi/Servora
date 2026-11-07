using Servora.Application.Common;

namespace Servora.Application.Portal;

/// <summary>
/// The current user's own incidents ("issues") and service requests in the self-service portal. Every method requires
/// <c>portal.access</c> and only ever returns or changes records the user is the caller / requested-for user of or opened;
/// other users' records are reported as not found.
/// </summary>
public interface IPortalTicketService
{
    /// <summary>Lists the user's incidents and requests, most recently updated first.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of items.</returns>
    Task<Result<PagedResult<PortalTicketSummary>>> ListAsync(PortalTicketQuery query, CancellationToken cancellationToken = default);

    /// <summary>Gets one of the user's incidents.</summary>
    /// <param name="id">The incident identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The incident.</returns>
    Task<Result<PortalIncidentDetail>> GetIncidentAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets one of the user's requests.</summary>
    /// <param name="id">The request identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The request.</returns>
    Task<Result<PortalRequestDetail>> GetRequestAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets the active top-level incident categories offered on the "Report an issue" form.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The category names in display order.</returns>
    Task<Result<IReadOnlyList<string>>> GetIssueCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Reports an issue: creates an incident with the current user as caller and the portal as channel.</summary>
    /// <param name="input">The issue.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created incident.</returns>
    Task<Result<PortalTicketSummary>> ReportIssueAsync(PortalIssueInput input, CancellationToken cancellationToken = default);

    /// <summary>Confirms the resolution of one of the user's resolved incidents and closes it.</summary>
    /// <param name="id">The incident identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> AcceptResolutionAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Reopens one of the user's resolved incidents because the problem persists.</summary>
    /// <param name="id">The incident identifier.</param>
    /// <param name="reason">Why the solution did not help (posted as a comment).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> ReopenIncidentAsync(Guid id, string reason, CancellationToken cancellationToken = default);

    /// <summary>Cancels one of the user's open requests.</summary>
    /// <param name="id">The request identifier.</param>
    /// <param name="reason">The reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> CancelRequestAsync(Guid id, string reason, CancellationToken cancellationToken = default);
}
