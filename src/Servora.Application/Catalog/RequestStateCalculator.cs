using Servora.Domain.Catalog;

namespace Servora.Application.Catalog;

/// <summary>Pure roll-up rules from catalog tasks to requested items and from requested items to requests.</summary>
public static class RequestStateCalculator
{
    /// <summary>Checks whether a task state is final.</summary>
    /// <param name="state">The state.</param>
    /// <returns><see langword="true"/> for completed and cancelled.</returns>
    public static bool IsFinal(CatalogTaskState state) => state is CatalogTaskState.Completed or CatalogTaskState.Cancelled;

    /// <summary>Checks whether a requested item state is final.</summary>
    /// <param name="state">The state.</param>
    /// <returns><see langword="true"/> for completed, rejected and cancelled.</returns>
    public static bool IsFinal(RequestItemState state) => state is RequestItemState.Completed or RequestItemState.Rejected or RequestItemState.Cancelled;

    /// <summary>Checks whether a request state is final.</summary>
    /// <param name="state">The state.</param>
    /// <returns><see langword="true"/> for completed, rejected and cancelled.</returns>
    public static bool IsFinal(RequestState state) => state is RequestState.Completed or RequestState.Rejected or RequestState.Cancelled;

    /// <summary>Derives the state of a requested item in fulfilment from its tasks.</summary>
    /// <param name="tasks">The task states.</param>
    /// <returns>
    /// <see langword="null"/> while tasks are open (or none exist); <see cref="RequestItemState.Completed"/> when all tasks
    /// are closed and at least one was completed; <see cref="RequestItemState.Cancelled"/> when every task was cancelled.
    /// </returns>
    public static RequestItemState? ItemStateFromTasks(IReadOnlyCollection<CatalogTaskState> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        if (tasks.Count == 0 || tasks.Any(t => !IsFinal(t)))
        {
            return null;
        }

        return tasks.Contains(CatalogTaskState.Completed) ? RequestItemState.Completed : RequestItemState.Cancelled;
    }

    /// <summary>Derives the state of a request from its requested items.</summary>
    /// <param name="items">The item states.</param>
    /// <returns>The request state.</returns>
    public static RequestState RequestStateFromItems(IReadOnlyCollection<RequestItemState> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            return RequestState.Submitted;
        }

        if (items.Any(i => !IsFinal(i)))
        {
            return items.All(i => i == RequestItemState.PendingApproval) ? RequestState.Submitted : RequestState.InProgress;
        }

        if (items.Contains(RequestItemState.Completed))
        {
            return RequestState.Completed;
        }

        return items.Contains(RequestItemState.Rejected) ? RequestState.Rejected : RequestState.Cancelled;
    }
}
