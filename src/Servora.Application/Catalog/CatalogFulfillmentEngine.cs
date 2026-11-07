using Microsoft.EntityFrameworkCore;
using Servora.Application.Activity;
using Servora.Application.Common;
using Servora.Application.Platform;
using Servora.Domain.Catalog;
using Servora.Domain.Common;

namespace Servora.Application.Catalog;

/// <summary>
/// Request fulfilment workflow: creates catalog tasks for approved requested items and rolls task states up to
/// requested items and requests. Performs <b>no permission checks</b>; callers (order service, request service,
/// approval handler) authorize first.
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="numbers">The number sequence service.</param>
/// <param name="activity">The activity stream service.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="clock">The clock.</param>
public sealed class CatalogFulfillmentEngine(
    IAppDbContextFactory dbFactory,
    INumberSequenceService numbers,
    IActivityService activity,
    ICurrentUser currentUser,
    IClock clock)
{
    private const int MaxItemsPerRequest = 100;
    private const int MaxTasksPerItem = 50;

    /// <summary>Starts fulfilment of a requested item: creates its catalog tasks and moves it to <c>InFulfillment</c>.</summary>
    /// <param name="requestItemId">The requested item identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of created tasks.</returns>
    public async Task<int> StartFulfillmentAsync(Guid requestItemId, CancellationToken cancellationToken = default)
    {
        Guid requestId;
        int created;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var item = await db.Set<RequestItem>().FirstOrDefaultAsync(r => r.Id == requestItemId, cancellationToken);
            if (item is null || RequestStateCalculator.IsFinal(item.State))
            {
                return 0;
            }

            requestId = item.RequestId;
            var catalogItem = await db.Set<CatalogItem>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == item.CatalogItemId, cancellationToken);
            var hasTasks = await db.Set<CatalogTask>().AnyAsync(t => t.RequestItemId == requestItemId, cancellationToken);
            var names = hasTasks ? [] : CatalogAdminService.ParseFulfillmentTasks(catalogItem?.FulfillmentTasks);
            if (!hasTasks && names.Count == 0)
            {
                names = [$"Fulfil {item.ShortDescription}"];
            }

            var now = clock.UtcNow;
            for (var i = 0; i < names.Count; i++)
            {
                db.Set<CatalogTask>().Add(new CatalogTask
                {
                    Number = await numbers.NextAsync(TableNames.CatalogTask, CatalogTables.CatalogTaskPrefix, cancellationToken),
                    RequestItemId = item.Id,
                    RequestId = item.RequestId,
                    ShortDescription = names[i].Length > 300 ? names[i][..300] : names[i],
                    Description = $"{item.Number} · {item.ShortDescription}",
                    AssignmentGroupId = catalogItem?.FulfillmentGroupId ?? item.AssignmentGroupId,
                    Priority = item.Priority,
                    Impact = item.Impact,
                    Urgency = item.Urgency,
                    OpenedAt = now,
                    OpenedById = currentUser.UserId,
                    DueAt = item.DueAt,
                    Order = (i + 1) * 10,
                });
            }

            created = names.Count;
            item.State = RequestItemState.InFulfillment;
            await db.SaveChangesAsync(cancellationToken);
        }

        if (created > 0)
        {
            await activity.AddSystemAsync(TableNames.RequestItem, requestItemId, $"Fulfilment started: {created} catalog task(s) created.", cancellationToken);
        }

        await RecomputeRequestAsync(requestId, cancellationToken);
        return created;
    }

    /// <summary>Closes a requested item as rejected after its approval was rejected.</summary>
    /// <param name="requestItemId">The requested item identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the item was closed.</returns>
    public async Task RejectAsync(Guid requestItemId, CancellationToken cancellationToken = default)
    {
        Guid requestId;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var item = await db.Set<RequestItem>().FirstOrDefaultAsync(r => r.Id == requestItemId, cancellationToken);
            if (item is null || RequestStateCalculator.IsFinal(item.State))
            {
                return;
            }

            requestId = item.RequestId;
            Close(item, RequestItemState.Rejected);
            await db.SaveChangesAsync(cancellationToken);
        }

        await activity.AddSystemAsync(TableNames.RequestItem, requestItemId, "Requested item rejected by the approvers.", cancellationToken);
        await RecomputeRequestAsync(requestId, cancellationToken);
    }

    /// <summary>Rolls the task states of a requested item up to the item (and its request).</summary>
    /// <param name="requestItemId">The requested item identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new item state when it changed; otherwise <see langword="null"/>.</returns>
    public async Task<RequestItemState?> RecomputeItemAsync(Guid requestItemId, CancellationToken cancellationToken = default)
    {
        Guid requestId;
        RequestItemState? newState;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var item = await db.Set<RequestItem>().FirstOrDefaultAsync(r => r.Id == requestItemId, cancellationToken);
            if (item is null || item.State != RequestItemState.InFulfillment)
            {
                return null;
            }

            requestId = item.RequestId;
            var states = await db.Set<CatalogTask>().Where(t => t.RequestItemId == requestItemId).Select(t => t.State).Take(MaxTasksPerItem).ToListAsync(cancellationToken);
            newState = RequestStateCalculator.ItemStateFromTasks(states);
            if (newState is null)
            {
                return null;
            }

            Close(item, newState.Value);
            await db.SaveChangesAsync(cancellationToken);
        }

        await activity.AddSystemAsync(TableNames.RequestItem, requestItemId, newState == RequestItemState.Completed ? "All tasks closed – item delivered." : "All tasks cancelled – item closed as cancelled.", cancellationToken);
        await RecomputeRequestAsync(requestId, cancellationToken);
        return newState;
    }

    /// <summary>Recalculates the state of a request from its requested items.</summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The request state.</returns>
    public async Task<RequestState?> RecomputeRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var request = await db.Set<Request>().FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null)
        {
            return null;
        }

        var states = await db.Set<RequestItem>().Where(r => r.RequestId == requestId).Select(r => r.State).Take(MaxItemsPerRequest).ToListAsync(cancellationToken);
        var state = RequestStateCalculator.RequestStateFromItems(states);
        if (state == request.State)
        {
            return state;
        }

        request.State = state;
        if (RequestStateCalculator.IsFinal(state))
        {
            request.Active = false;
            request.ClosedAt = clock.UtcNow;
            request.ClosedById ??= currentUser.UserId;
        }

        await db.SaveChangesAsync(cancellationToken);
        if (RequestStateCalculator.IsFinal(state))
        {
            await activity.AddSystemAsync(TableNames.Request, requestId, $"Request closed: {state}.", cancellationToken);
        }

        return state;
    }

    /// <summary>Cancels a request: open tasks and items are cancelled and pending approvals must be withdrawn by the caller.</summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="reason">The reason shown in the activity stream.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifiers of the requested items that were cancelled.</returns>
    public async Task<IReadOnlyList<Guid>> CancelRequestAsync(Guid requestId, string reason, CancellationToken cancellationToken = default)
    {
        List<Guid> cancelled;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var items = await db.Set<RequestItem>().Where(r => r.RequestId == requestId).Take(MaxItemsPerRequest).ToListAsync(cancellationToken);
            var openItems = items.Where(i => !RequestStateCalculator.IsFinal(i.State)).ToList();
            var openIds = openItems.Select(i => i.Id).ToList();
            var tasks = await db.Set<CatalogTask>().Where(t => openIds.Contains(t.RequestItemId) && (t.State == CatalogTaskState.Open || t.State == CatalogTaskState.WorkInProgress))
                .Take(MaxItemsPerRequest * MaxTasksPerItem)
                .ToListAsync(cancellationToken);
            var now = clock.UtcNow;
            foreach (var task in tasks)
            {
                task.State = CatalogTaskState.Cancelled;
                task.Active = false;
                task.ClosedAt = now;
                task.ClosedById = currentUser.UserId;
                task.CloseNotes = "Request cancelled.";
            }

            foreach (var item in openItems)
            {
                if (item.Approval == Domain.Approvals.ApprovalStatus.Requested)
                {
                    item.Approval = Domain.Approvals.ApprovalStatus.Cancelled;
                }

                Close(item, RequestItemState.Cancelled);
            }

            cancelled = openIds;
            await db.SaveChangesAsync(cancellationToken);
        }

        await activity.AddSystemAsync(TableNames.Request, requestId, $"Request cancelled by {currentUser.DisplayName}: {reason}", cancellationToken);
        await RecomputeRequestAsync(requestId, cancellationToken);
        return cancelled;
    }

    private void Close(RequestItem item, RequestItemState state)
    {
        item.State = state;
        item.Active = false;
        item.ClosedAt = clock.UtcNow;
        item.ClosedById = currentUser.UserId;
    }
}
