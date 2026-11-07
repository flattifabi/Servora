using Servora.Application.Common;
using Servora.Application.Directory;
using Servora.Domain.Itsm;

namespace Servora.Application.Itsm;

/// <summary>
/// Use cases of Problem Management. Every method checks the <c>problem.*</c> permissions of the current actor (and
/// <c>incident.write</c> when incidents are linked). State changes are validated by <see cref="ProblemStateMachine"/>.
/// </summary>
public interface IProblemService
{
    /// <summary>Lists problems (server-side paging, sorting and search).</summary>
    /// <param name="filter">The quick filter.</param>
    /// <param name="request">Paging, sort (<c>Number</c>, <c>Priority</c>, <c>State</c>, <c>ShortDescription</c>, <c>Category</c>, <c>OpenedAt</c>, <c>UpdatedAt</c>) and search.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page.</returns>
    Task<Result<PagedResult<ProblemListItem>>> ListAsync(ProblemQuickFilter filter, PageRequest request, CancellationToken cancellationToken = default);

    /// <summary>Gets the key figures for the list header.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The figures.</returns>
    Task<Result<ProblemStats>> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a problem with display values.</summary>
    /// <param name="id">The problem identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The problem.</returns>
    Task<Result<ProblemDetails>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Searches active problems by number or summary (for reference pickers).</summary>
    /// <param name="term">The search term.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Up to 10 matches.</returns>
    Task<Result<IReadOnlyList<LookupItem>>> SearchAsync(string? term, CancellationToken cancellationToken = default);

    /// <summary>Creates a problem in state New. The number is allocated with the <c>PRB</c> prefix.</summary>
    /// <param name="input">The field values.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new problem identifier.</returns>
    Task<Result<Guid>> CreateAsync(ProblemInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a problem from an incident: copies summary, description, category, impact, urgency, CI, business service
    /// and assignment group, and links the incident to the new problem.
    /// </summary>
    /// <param name="incidentId">The incident identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new problem identifier.</returns>
    Task<Result<Guid>> CreateFromIncidentAsync(Guid incidentId, CancellationToken cancellationToken = default);

    /// <summary>Updates the editable fields of a problem.</summary>
    /// <param name="id">The problem identifier.</param>
    /// <param name="version">The version the user edited (optimistic concurrency).</param>
    /// <param name="input">The field values.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UpdateAsync(Guid id, long version, ProblemInput input, CancellationToken cancellationToken = default);

    /// <summary>Moves a problem to Assess, Root Cause Analysis or Fix in Progress.</summary>
    /// <param name="id">The problem identifier.</param>
    /// <param name="target">The target state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> MoveToAsync(Guid id, ProblemState target, CancellationToken cancellationToken = default);

    /// <summary>Resolves a problem.</summary>
    /// <param name="id">The problem identifier.</param>
    /// <param name="code">The resolution code.</param>
    /// <param name="fixNotes">The fix notes (required).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> ResolveAsync(Guid id, ProblemResolutionCode code, string fixNotes, CancellationToken cancellationToken = default);

    /// <summary>Closes a resolved problem.</summary>
    /// <param name="id">The problem identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> CloseAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Reopens a resolved problem (back to Fix in Progress).</summary>
    /// <param name="id">The problem identifier.</param>
    /// <param name="reason">Why the problem is reopened (required).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> ReopenAsync(Guid id, string reason, CancellationToken cancellationToken = default);

    /// <summary>Cancels an open problem.</summary>
    /// <param name="id">The problem identifier.</param>
    /// <param name="reason">Why the problem is cancelled (required).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> CancelAsync(Guid id, string reason, CancellationToken cancellationToken = default);

    /// <summary>Assigns the problem to the current user.</summary>
    /// <param name="id">The problem identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> AssignToMeAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Assigns several active problems at once (bulk assign, max 200).</summary>
    /// <param name="ids">The problem identifiers.</param>
    /// <param name="groupId">The new assignment group, or <see langword="null"/> to keep the current group.</param>
    /// <param name="userId">The new assignee, or <see langword="null"/> to keep the current assignee.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of updated problems.</returns>
    Task<Result<int>> AssignAsync(IReadOnlyCollection<Guid> ids, Guid? groupId, Guid? userId, CancellationToken cancellationToken = default);

    /// <summary>Links incidents to a problem (max 100 per call).</summary>
    /// <param name="id">The problem identifier.</param>
    /// <param name="incidentIds">The incident identifiers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of linked incidents.</returns>
    Task<Result<int>> LinkIncidentsAsync(Guid id, IReadOnlyCollection<Guid> incidentIds, CancellationToken cancellationToken = default);

    /// <summary>Removes the link between an incident and the problem.</summary>
    /// <param name="id">The problem identifier.</param>
    /// <param name="incidentId">The incident identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UnlinkIncidentAsync(Guid id, Guid incidentId, CancellationToken cancellationToken = default);

    /// <summary>Deletes a problem and removes its incident links (requires <c>problem.delete</c>).</summary>
    /// <param name="id">The problem identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
