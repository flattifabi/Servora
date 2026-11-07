using Servora.Application.Common;
using Servora.Application.Events;
using Servora.Domain.Changes;
using Servora.Domain.Itsm;

namespace Servora.Application.Changes;

/// <summary>
/// Before-save hook for change requests and change tasks. It runs for every writer (the change service, the generic
/// record API, flows, imports) and guarantees the module invariants centrally:
/// <list type="bullet">
/// <item>the priority is calculated from impact and urgency with <see cref="PriorityMatrix"/>,</item>
/// <item>state changes of a change request are validated by <see cref="ChangeStateMachine"/> including the mandatory
/// fields of the target state (planning, plans, risk assessment, CAB approval, close code and notes),</item>
/// <item><see cref="TaskRecord.Active"/> and <see cref="TaskRecord.ClosedAt"/> follow the state.</item>
/// </list>
/// Checks that need other records (open change tasks before closing) stay in <see cref="ChangeService"/>.
/// </summary>
/// <param name="clock">The clock.</param>
public sealed class ChangeSavingHook(IClock clock) : IRecordSavingHook
{
    /// <inheritdoc />
    /// <remarks>Runs early so that later hooks (business rules) see the calculated priority.</remarks>
    public int Order => 10;

    /// <inheritdoc />
    public Task OnSavingAsync(RecordSavingContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ChangeType == RecordChangeType.Deleted)
        {
            return Task.CompletedTask;
        }

        switch (context.Entity)
        {
            case ChangeRequest change:
                ApplyPriority(context, change);
                HandleChange(context, change);
                break;
            case ChangeTask task:
                ApplyPriority(context, task);
                HandleTask(task);
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

    private void HandleChange(RecordSavingContext context, ChangeRequest change)
    {
        if (context.ChangeType == RecordChangeType.Updated
            && context.HasChanged(nameof(ChangeRequest.State))
            && TryGetOldValue(context, nameof(ChangeRequest.State), out ChangeState from)
            && from != change.State)
        {
            var error = ChangeStateMachine.Validate(new ChangeTransitionContext
            {
                Type = change.Type,
                Current = from,
                Target = change.State,
                PlannedStartAt = change.PlannedStartAt,
                PlannedEndAt = change.PlannedEndAt,
                HasImplementationPlan = !string.IsNullOrWhiteSpace(change.ImplementationPlan),
                HasBackoutPlan = !string.IsNullOrWhiteSpace(change.BackoutPlan),
                RiskAssessed = change.RiskAssessedAt.HasValue,
                Approval = change.Approval,
                OpenTaskCount = 0,
                CloseCode = change.CloseCode,
                CloseNotes = change.CloseNotes,
            });
            if (error is not null)
            {
                context.Abort(error);
                return;
            }
        }

        var now = clock.UtcNow;
        if (change.OpenedAt == default)
        {
            change.OpenedAt = now;
        }

        change.Active = !ChangeStateMachine.IsFinal(change.State);
        if (change.Active)
        {
            change.ClosedAt = null;
            change.ClosedById = null;
        }
        else
        {
            change.ClosedAt ??= now;
        }
    }

    private void HandleTask(ChangeTask task)
    {
        var now = clock.UtcNow;
        if (task.OpenedAt == default)
        {
            task.OpenedAt = now;
        }

        task.Active = task.State is ChangeTaskState.Open or ChangeTaskState.InProgress;
        if (task.Active)
        {
            task.ClosedAt = null;
            task.ClosedById = null;
        }
        else
        {
            task.ClosedAt ??= now;
        }
    }

    private static bool TryGetOldValue<TEnum>(RecordSavingContext context, string field, out TEnum value)
        where TEnum : struct, Enum
    {
        var change = context.Changes.FirstOrDefault(c => string.Equals(c.Field, field, StringComparison.OrdinalIgnoreCase));
        switch (change?.OldValue)
        {
            case TEnum typed:
                value = typed;
                return true;
            case null:
                value = default;
                return false;
            case var other when Enum.TryParse(other.ToString(), out TEnum parsed):
                value = parsed;
                return true;
            default:
                value = default;
                return false;
        }
    }
}
