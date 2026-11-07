using Servora.Application.Common;
using Servora.Application.Directory;
using Servora.Domain.Itsm;

namespace Servora.Application.Itsm;

/// <summary>
/// Use cases of Incident Management. Every method checks the <c>incident.*</c> permissions of the current actor and
/// returns <see cref="Result"/>s for expected failures. State changes are validated by
/// <see cref="IncidentStateMachine"/> (also enforced by <see cref="ItsmSavingHook"/> for every other writer).
/// </summary>
public interface IIncidentService
{
    /// <summary>Lists incidents (server-side paging, sorting and search).</summary>
    /// <param name="filter">The quick filter.</param>
    /// <param name="request">Paging, sort (<c>Number</c>, <c>Priority</c>, <c>State</c>, <c>ShortDescription</c>, <c>Category</c>, <c>OpenedAt</c>, <c>UpdatedAt</c>) and search.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page.</returns>
    Task<Result<PagedResult<IncidentListItem>>> ListAsync(IncidentQuickFilter filter, PageRequest request, CancellationToken cancellationToken = default);

    /// <summary>Gets the key figures for the list header.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The figures.</returns>
    Task<Result<IncidentStats>> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets an incident with display values.</summary>
    /// <param name="id">The incident identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The incident.</returns>
    Task<Result<IncidentDetails>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets the selectable categories and subcategories.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The categories in display order.</returns>
    Task<Result<IReadOnlyList<CategoryOption>>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Searches incidents by number or summary (for reference pickers).</summary>
    /// <param name="term">The search term.</param>
    /// <param name="excludeId">An incident to exclude (for example the record itself).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Up to 10 matches.</returns>
    Task<Result<IReadOnlyList<LookupItem>>> SearchAsync(string? term, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Creates an incident in state New. The number is allocated with the <c>INC</c> prefix.</summary>
    /// <param name="input">The field values.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new incident identifier.</returns>
    Task<Result<Guid>> CreateAsync(IncidentInput input, CancellationToken cancellationToken = default);

    /// <summary>Updates the editable fields of an incident.</summary>
    /// <param name="id">The incident identifier.</param>
    /// <param name="version">The version the user edited (optimistic concurrency).</param>
    /// <param name="input">The field values.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UpdateAsync(Guid id, long version, IncidentInput input, CancellationToken cancellationToken = default);

    /// <summary>Moves a new or on-hold incident to In Progress.</summary>
    /// <param name="id">The incident identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> StartWorkAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Puts an incident on hold.</summary>
    /// <param name="id">The incident identifier.</param>
    /// <param name="reason">The on-hold reason.</param>
    /// <param name="note">An optional work note.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> PutOnHoldAsync(Guid id, OnHoldReason reason, string? note, CancellationToken cancellationToken = default);

    /// <summary>Resolves an incident.</summary>
    /// <param name="id">The incident identifier.</param>
    /// <param name="code">The resolution code.</param>
    /// <param name="notes">The resolution notes (required).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> ResolveAsync(Guid id, IncidentResolutionCode code, string notes, CancellationToken cancellationToken = default);

    /// <summary>Closes a resolved incident.</summary>
    /// <param name="id">The incident identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> CloseAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Reopens a resolved incident (back to In Progress, increments the reopen count).</summary>
    /// <param name="id">The incident identifier.</param>
    /// <param name="reason">Why the incident is reopened (required).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> ReopenAsync(Guid id, string reason, CancellationToken cancellationToken = default);

    /// <summary>Cancels an open incident.</summary>
    /// <param name="id">The incident identifier.</param>
    /// <param name="reason">Why the incident is cancelled (required).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> CancelAsync(Guid id, string reason, CancellationToken cancellationToken = default);

    /// <summary>Assigns the incident to the current user.</summary>
    /// <param name="id">The incident identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> AssignToMeAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Assigns several active incidents at once (bulk assign, max 200).</summary>
    /// <param name="ids">The incident identifiers.</param>
    /// <param name="groupId">The new assignment group, or <see langword="null"/> to keep the current group.</param>
    /// <param name="userId">The new assignee, or <see langword="null"/> to keep the current assignee.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of updated incidents.</returns>
    Task<Result<int>> AssignAsync(IReadOnlyCollection<Guid> ids, Guid? groupId, Guid? userId, CancellationToken cancellationToken = default);

    /// <summary>Links an incident to a problem or removes the link.</summary>
    /// <param name="id">The incident identifier.</param>
    /// <param name="problemId">The problem identifier, or <see langword="null"/> to unlink.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> LinkProblemAsync(Guid id, Guid? problemId, CancellationToken cancellationToken = default);

    /// <summary>Gets the child incidents of a (major) incident.</summary>
    /// <param name="parentId">The parent incident identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Up to 100 child incidents.</returns>
    Task<Result<IReadOnlyList<IncidentListItem>>> GetChildrenAsync(Guid parentId, CancellationToken cancellationToken = default);

    /// <summary>Gets the incidents linked to a problem.</summary>
    /// <param name="problemId">The problem identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Up to 200 incidents.</returns>
    Task<Result<IReadOnlyList<IncidentListItem>>> GetByProblemAsync(Guid problemId, CancellationToken cancellationToken = default);

    /// <summary>Deletes an incident (requires <c>incident.delete</c>).</summary>
    /// <param name="id">The incident identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
