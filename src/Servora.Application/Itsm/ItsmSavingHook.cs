using Servora.Application.Common;
using Servora.Application.Events;
using Servora.Domain.Itsm;

namespace Servora.Application.Itsm;

/// <summary>
/// Before-save hook for incidents and problems. It runs for every writer (application services, the generic record
/// API, flows, imports) and therefore guarantees the module invariants centrally:
/// <list type="bullet">
/// <item>the priority is calculated from impact and urgency with <see cref="PriorityMatrix"/> on insert and whenever
/// impact or urgency change,</item>
/// <item>state transitions are validated by <see cref="IncidentStateMachine"/> / <see cref="ProblemStateMachine"/>
/// including the mandatory fields of the target state (invalid saves are aborted),</item>
/// <item><see cref="TaskRecord.Active"/>, resolution and closure timestamps are maintained.</item>
/// </list>
/// </summary>
/// <param name="clock">The clock.</param>
public sealed class ItsmSavingHook(IClock clock) : IRecordSavingHook
{
    /// <inheritdoc />
    /// <remarks>Runs early so that later hooks (business rules) see the calculated priority.</remarks>
    public int Order => 10;

    /// <inheritdoc />
    public Task OnSavingAsync(RecordSavingContext context, CancellationToken cancellationToken)
    {
        if (context.ChangeType == RecordChangeType.Deleted)
        {
            return Task.CompletedTask;
        }

        switch (context.Entity)
        {
            case Incident incident:
                ApplyPriority(context, incident);
                HandleIncident(context, incident);
                break;
            case Problem problem:
                ApplyPriority(context, problem);
                HandleProblem(context, problem);
                break;
        }

        return Task.CompletedTask;
    }

    private static void ApplyPriority(RecordSavingContext context, TaskRecord task)
    {
        if (context.ChangeType == RecordChangeType.Created
            || context.HasChanged(nameof(TaskRecord.Impact))
            || context.HasChanged(nameof(TaskRecord.Urgency))
            || context.HasChanged(nameof(TaskRecord.Priority)))
        {
            task.Priority = PriorityMatrix.Calculate(task.Impact, task.Urgency);
        }
    }

    private void HandleIncident(RecordSavingContext context, Incident incident)
    {
        string? error;
        if (context.ChangeType == RecordChangeType.Updated && TryGetOldValue(context, nameof(Incident.State), out IncidentState from))
        {
            error = IncidentStateMachine.ValidateTransition(from, incident);
        }
        else
        {
            error = IncidentStateMachine.ValidateStateFields(incident);
        }

        if (error is not null)
        {
            context.Abort(error);
            return;
        }

        var now = clock.UtcNow;
        incident.Active = IncidentStateMachine.IsActive(incident.State);
        if (incident.State != IncidentState.OnHold)
        {
            incident.OnHoldReason = null;
        }

        if (incident.State is IncidentState.Resolved or IncidentState.Closed)
        {
            incident.ResolvedAt ??= now;
        }
        else if (incident.State != IncidentState.Cancelled)
        {
            incident.ResolvedAt = null;
            incident.ResolvedById = null;
        }

        if (IncidentStateMachine.IsFinal(incident.State))
        {
            incident.ClosedAt ??= now;
        }
        else
        {
            incident.ClosedAt = null;
            incident.ClosedById = null;
        }

        if (incident.OpenedAt == default)
        {
            incident.OpenedAt = now;
        }
    }

    private void HandleProblem(RecordSavingContext context, Problem problem)
    {
        string? error;
        if (context.ChangeType == RecordChangeType.Updated && TryGetOldValue(context, nameof(Problem.State), out ProblemState from))
        {
            error = ProblemStateMachine.ValidateTransition(from, problem);
        }
        else
        {
            error = ProblemStateMachine.ValidateStateFields(problem);
        }

        if (error is not null)
        {
            context.Abort(error);
            return;
        }

        var now = clock.UtcNow;
        problem.Active = ProblemStateMachine.IsActive(problem.State);
        if (problem.State is ProblemState.Resolved or ProblemState.Closed)
        {
            problem.ResolvedAt ??= now;
        }
        else if (problem.State != ProblemState.Cancelled)
        {
            problem.ResolvedAt = null;
            problem.ResolvedById = null;
        }

        if (ProblemStateMachine.IsFinal(problem.State))
        {
            problem.ClosedAt ??= now;
        }
        else
        {
            problem.ClosedAt = null;
            problem.ClosedById = null;
        }

        if (problem.OpenedAt == default)
        {
            problem.OpenedAt = now;
        }
    }

    private static bool TryGetOldValue<TEnum>(RecordSavingContext context, string field, out TEnum value)
        where TEnum : struct, Enum
    {
        var change = context.Changes.FirstOrDefault(c => string.Equals(c.Field, field, StringComparison.OrdinalIgnoreCase));
        if (change?.OldValue is TEnum typed)
        {
            value = typed;
            return true;
        }

        if (change?.OldValue is not null && Enum.TryParse(change.OldValue.ToString(), out TEnum parsed))
        {
            value = parsed;
            return true;
        }

        value = default;
        return false;
    }
}
