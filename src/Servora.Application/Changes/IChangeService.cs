using Servora.Application.Common;
using Servora.Domain.Changes;

namespace Servora.Application.Changes;

/// <summary>Change management use cases: change requests, risk assessment, lifecycle, conflicts, tasks and calendar.</summary>
public interface IChangeService
{
    /// <summary>Lists change requests (<c>change.read</c>).</summary>
    /// <param name="filter">The predefined filter.</param>
    /// <param name="page">Paging, sorting (<c>Number</c>, <c>State</c>, <c>PlannedStart</c>, <c>Priority</c>, <c>Updated</c>) and search.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of changes.</returns>
    Task<Result<PagedResult<ChangeListItem>>> ListAsync(ChangeListFilter filter, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Gets key figures (<c>change.read</c>).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The statistics.</returns>
    Task<Result<ChangeStats>> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a change request with tasks and conflicts (<c>change.read</c>).</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The change.</returns>
    Task<Result<ChangeDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a change request in state <c>New</c> (<c>change.create</c>).</summary>
    /// <param name="model">The fields.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new identifier.</returns>
    Task<Result<Guid>> CreateAsync(ChangeEditModel model, CancellationToken cancellationToken = default);

    /// <summary>Updates a change request (<c>change.write</c>).</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="model">The fields.</param>
    /// <param name="expectedVersion">The version the user edited (optimistic concurrency).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UpdateAsync(Guid id, ChangeEditModel model, long expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>Stores a completed risk assessment and recalculates the risk (<c>change.write</c>).</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="answers">The answers (question key to option index).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The calculated risk.</returns>
    Task<Result<RiskAssessmentResult>> AssessRiskAsync(Guid id, IReadOnlyDictionary<string, int> answers, CancellationToken cancellationToken = default);

    /// <summary>Moves a change to another state (<c>change.write</c>). Moving to <c>Authorize</c> requests CAB approval.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="target">The target state.</param>
    /// <param name="input">Additional input (note, close code and notes).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> TransitionAsync(Guid id, ChangeState target, ChangeTransitionInput input, CancellationToken cancellationToken = default);

    /// <summary>Finds active changes on the same configuration item with an overlapping planned window (<c>change.read</c>).</summary>
    /// <param name="configurationItemId">The configuration item.</param>
    /// <param name="start">The planned start.</param>
    /// <param name="end">The planned end.</param>
    /// <param name="excludeChangeId">A change to exclude (the change being edited).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The conflicts.</returns>
    Task<Result<IReadOnlyList<ChangeConflict>>> FindConflictsAsync(Guid configurationItemId, DateTimeOffset start, DateTimeOffset end, Guid? excludeChangeId, CancellationToken cancellationToken = default);

    /// <summary>Gets changes whose planned window overlaps a period (<c>change.read</c>).</summary>
    /// <param name="from">The period start (UTC).</param>
    /// <param name="to">The period end (UTC, exclusive).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entries (at most 500).</returns>
    Task<Result<IReadOnlyList<ChangeCalendarEntry>>> GetCalendarAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    /// <summary>Adds a change task (<c>change.write</c>).</summary>
    /// <param name="changeId">The change identifier.</param>
    /// <param name="model">The task fields.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task identifier.</returns>
    Task<Result<Guid>> AddTaskAsync(Guid changeId, ChangeTaskEditModel model, CancellationToken cancellationToken = default);

    /// <summary>Updates a change task (<c>change.write</c>).</summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="model">The task fields.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UpdateTaskAsync(Guid taskId, ChangeTaskEditModel model, CancellationToken cancellationToken = default);

    /// <summary>Changes the state of a change task (<c>change.write</c>).</summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="state">The new state.</param>
    /// <param name="closeNotes">Close notes for completed / cancelled tasks.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> SetTaskStateAsync(Guid taskId, ChangeTaskState state, string? closeNotes, CancellationToken cancellationToken = default);

    /// <summary>Deletes a change request that is new or cancelled, including its tasks (<c>change.delete</c>).</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
