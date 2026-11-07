using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Activity;
using Servora.Application.Approvals;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Activity;
using Servora.Domain.Catalog;
using Servora.Domain.Common;
using Servora.Domain.Identity;

namespace Servora.Application.Catalog;

/// <summary>Default <see cref="IRequestService"/> implementation.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="clock">The clock.</param>
/// <param name="activity">The activity stream service.</param>
/// <param name="fulfillment">The fulfilment engine.</param>
/// <param name="approvals">The approval engine.</param>
public sealed class RequestService(
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    IClock clock,
    IActivityService activity,
    CatalogFulfillmentEngine fulfillment,
    ApprovalEngine approvals) : IRequestService
{
    private const int MaxItems = 100;
    private const int MaxTasks = 500;

    /// <inheritdoc />
    public async Task<Result<PagedResult<RequestListItem>>> ListRequestsAsync(RequestListFilter filter, PageRequest page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!await currentUser.HasPermissionAsync(Permissions.Requests.Read, cancellationToken))
        {
            return Result<PagedResult<RequestListItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<Request>().AsNoTracking();
        query = filter switch
        {
            RequestListFilter.AwaitingApproval => query.Where(r => r.State == RequestState.Submitted),
            RequestListFilter.Closed => query.Where(r => !r.Active),
            RequestListFilter.All => query,
            _ => query.Where(r => r.Active),
        };
        return Result<PagedResult<RequestListItem>>.Success(await PageRequestsAsync(db, query, page, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<RequestListItem>>> ListMyRequestsAsync(PageRequest page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (currentUser.UserId is not { } me)
        {
            return Result<PagedResult<RequestListItem>>.Forbidden("Only users have personal requests.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<Request>().AsNoTracking().Where(r => r.RequestedForId == me || r.OpenedById == me);
        return Result<PagedResult<RequestListItem>>.Success(await PageRequestsAsync(db, query, page, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<RequestItemListItem>>> ListItemsAsync(RequestListFilter filter, PageRequest page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!await currentUser.HasPermissionAsync(Permissions.Requests.Read, cancellationToken))
        {
            return Result<PagedResult<RequestItemListItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var me = currentUser.UserId;
        var myGroups = db.Set<GroupMember>().Where(m => m.UserId == me).Select(m => m.GroupId);
        var query = db.Set<RequestItem>().AsNoTracking();
        query = filter switch
        {
            RequestListFilter.AwaitingApproval => query.Where(r => r.State == RequestItemState.PendingApproval),
            RequestListFilter.MyGroups or RequestListFilter.AssignedToMe => query.Where(r => r.Active && r.AssignmentGroupId != null && myGroups.Contains(r.AssignmentGroupId.Value)),
            RequestListFilter.Closed => query.Where(r => !r.Active),
            RequestListFilter.All => query,
            _ => query.Where(r => r.Active),
        };

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim().ToLower();
            query = query.Where(r => r.Number.ToLower().Contains(term) || r.ShortDescription.ToLower().Contains(term));
        }

        query = (page.SortBy, page.SortDescending) switch
        {
            ("State", false) => query.OrderBy(r => r.State).ThenByDescending(r => r.Number),
            ("State", true) => query.OrderByDescending(r => r.State).ThenByDescending(r => r.Number),
            ("Opened", false) => query.OrderBy(r => r.OpenedAt),
            ("Number", false) => query.OrderBy(r => r.Number),
            _ => query.OrderByDescending(r => r.Number),
        };

        var paged = await query.ToPagedResultAsync(page, cancellationToken);
        var users = await ReferenceNameResolver.UsersAsync(db, paged.Items.Select(r => r.RequestedForId), cancellationToken);
        var groups = await ReferenceNameResolver.GroupsAsync(db, paged.Items.Select(r => r.AssignmentGroupId), cancellationToken);
        var requestIds = paged.Items.Select(r => r.RequestId).Distinct().ToList();
        var numbers = await db.Set<Request>().AsNoTracking().Where(r => requestIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Number, cancellationToken);
        var rows = paged.Items.Select(r => new RequestItemListItem
        {
            Id = r.Id,
            Number = r.Number,
            RequestId = r.RequestId,
            RequestNumber = numbers.GetValueOrDefault(r.RequestId),
            ItemName = r.ShortDescription,
            Quantity = r.Quantity,
            RequestedForName = users.NameOf(r.RequestedForId),
            State = r.State,
            Approval = r.Approval,
            AssignmentGroupName = groups.NameOf(r.AssignmentGroupId),
            OpenedAt = r.OpenedAt,
            DueAt = r.DueAt,
        }).ToList();
        return Result<PagedResult<RequestItemListItem>>.Success(new PagedResult<RequestItemListItem>(rows, paged.TotalCount, paged.Page, paged.PageSize));
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<CatalogTaskListItem>>> ListTasksAsync(RequestListFilter filter, PageRequest page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!await currentUser.HasPermissionAsync(Permissions.Requests.Read, cancellationToken))
        {
            return Result<PagedResult<CatalogTaskListItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var me = currentUser.UserId;
        var myGroups = db.Set<GroupMember>().Where(m => m.UserId == me).Select(m => m.GroupId);
        var query = db.Set<CatalogTask>().AsNoTracking();
        query = filter switch
        {
            RequestListFilter.MyGroups => query.Where(t => t.Active && t.AssignmentGroupId != null && myGroups.Contains(t.AssignmentGroupId.Value)),
            RequestListFilter.AssignedToMe => query.Where(t => t.Active && t.AssignedToId == me),
            RequestListFilter.Closed => query.Where(t => !t.Active),
            RequestListFilter.All => query,
            _ => query.Where(t => t.Active),
        };

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim().ToLower();
            query = query.Where(t => t.Number.ToLower().Contains(term) || t.ShortDescription.ToLower().Contains(term));
        }

        query = (page.SortBy, page.SortDescending) switch
        {
            ("State", false) => query.OrderBy(t => t.State).ThenByDescending(t => t.Number),
            ("State", true) => query.OrderByDescending(t => t.State).ThenByDescending(t => t.Number),
            ("Priority", false) => query.OrderBy(t => t.Priority).ThenByDescending(t => t.Number),
            ("Priority", true) => query.OrderByDescending(t => t.Priority).ThenByDescending(t => t.Number),
            ("Number", false) => query.OrderBy(t => t.Number),
            _ => query.OrderByDescending(t => t.Number),
        };

        var paged = await query.ToPagedResultAsync(page, cancellationToken);
        var rows = await MapTasksAsync(db, paged.Items, cancellationToken);
        return Result<PagedResult<CatalogTaskListItem>>.Success(new PagedResult<CatalogTaskListItem>(rows, paged.TotalCount, paged.Page, paged.PageSize));
    }

    /// <inheritdoc />
    public async Task<Result<RequestStats>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Requests.Read, cancellationToken))
        {
            return Result<RequestStats>.Forbidden();
        }

        var since = clock.UtcNow.AddDays(-30);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var tasks = db.Set<CatalogTask>().AsNoTracking();
        return Result<RequestStats>.Success(new RequestStats(
            await db.Set<Request>().CountAsync(r => r.Active, cancellationToken),
            await db.Set<RequestItem>().CountAsync(r => r.State == RequestItemState.PendingApproval, cancellationToken),
            await tasks.CountAsync(t => t.Active, cancellationToken),
            await tasks.CountAsync(t => t.Active && t.AssignedToId == null, cancellationToken),
            await db.Set<Request>().CountAsync(r => r.State == RequestState.Completed && r.ClosedAt >= since, cancellationToken)));
    }

    /// <inheritdoc />
    public async Task<Result<RequestDetail>> GetAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var canRead = await currentUser.HasPermissionAsync(Permissions.Requests.Read, cancellationToken);
        var canWrite = await currentUser.HasPermissionAsync(Permissions.Requests.Write, cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var request = await db.Set<Request>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null)
        {
            return canRead ? Result<RequestDetail>.NotFound("The request was not found.") : Result<RequestDetail>.Forbidden();
        }

        var own = currentUser.UserId is { } me && (request.RequestedForId == me || request.OpenedById == me);
        if (!canRead && !own)
        {
            return Result<RequestDetail>.Forbidden();
        }

        var items = await db.Set<RequestItem>().AsNoTracking().Where(r => r.RequestId == requestId).OrderBy(r => r.Number).Take(MaxItems).ToListAsync(cancellationToken);
        var itemIds = items.Select(i => i.Id).ToList();
        var catalogIds = items.Select(i => i.CatalogItemId).Distinct().ToList();
        var catalogItems = await db.Set<CatalogItem>().AsNoTracking().Where(c => catalogIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);
        var variables = await db.Set<CatalogVariable>().AsNoTracking().Where(v => catalogIds.Contains(v.CatalogItemId)).Take(MaxItems * CatalogVariableValidator.MaxVariablesPerItem).ToListAsync(cancellationToken);
        var tasks = await db.Set<CatalogTask>().AsNoTracking().Where(t => itemIds.Contains(t.RequestItemId)).OrderBy(t => t.Order).ThenBy(t => t.Number).Take(MaxTasks).ToListAsync(cancellationToken);
        var answersByItem = items.ToDictionary(i => i.Id, i => ParseAnswers(i.VariablesJson));

        var referencedUsers = variables.Where(v => v.Type == CatalogVariableType.UserReference)
            .SelectMany(v => answersByItem.Values.Select(a => a.GetValueOrDefault(v.Name)))
            .Select(value => Guid.TryParse(value, out var id) ? id : (Guid?)null);
        var users = await ReferenceNameResolver.UsersAsync(db, [request.RequestedForId, request.OpenedById, .. referencedUsers], cancellationToken);
        var groups = await ReferenceNameResolver.GroupsAsync(db, items.Select(i => i.AssignmentGroupId), cancellationToken);
        var taskRows = await MapTasksAsync(db, tasks, cancellationToken);

        return Result<RequestDetail>.Success(new RequestDetail
        {
            Id = request.Id,
            Number = request.Number,
            ShortDescription = request.ShortDescription,
            State = request.State,
            RequestedForId = request.RequestedForId,
            RequestedForName = users.NameOf(request.RequestedForId),
            OpenedByName = users.NameOf(request.OpenedById),
            OpenedAt = request.OpenedAt,
            DueAt = request.DueAt,
            ClosedAt = request.ClosedAt,
            TotalPrice = request.TotalPrice,
            CanFulfil = canWrite,
            CanCancel = !RequestStateCalculator.IsFinal(request.State) && (canWrite || own),
            Items = items.Select(i => new RequestItemDetail
            {
                Id = i.Id,
                Number = i.Number,
                CatalogItemId = i.CatalogItemId,
                ItemName = i.ShortDescription,
                Icon = catalogItems.GetValueOrDefault(i.CatalogItemId)?.Icon ?? "package",
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                State = i.State,
                Approval = i.Approval,
                AssignmentGroupName = groups.NameOf(i.AssignmentGroupId),
                OpenedAt = i.OpenedAt,
                ClosedAt = i.ClosedAt,
                Answers = BuildAnswers(variables.Where(v => v.CatalogItemId == i.CatalogItemId).OrderBy(v => v.Order).ToList(), answersByItem[i.Id], users),
                Tasks = taskRows.Where(t => t.RequestItemId == i.Id).ToList(),
            }).ToList(),
        });
    }

    /// <inheritdoc />
    public async Task<Result> UpdateTaskAsync(Guid taskId, CatalogTaskUpdate update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (!await currentUser.HasPermissionAsync(Permissions.Requests.Write, cancellationToken))
        {
            return Result.Forbidden();
        }

        if (!Enum.IsDefined(update.State))
        {
            return Result.Invalid("The task state is invalid.");
        }

        if (update.CloseNotes is { Length: > 4000 } || update.WorkNote is { Length: > 4000 })
        {
            return Result.Invalid("Notes must not exceed 4000 characters.");
        }

        CatalogTask? task;
        bool closed;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            task = await db.Set<CatalogTask>().FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
            if (task is null)
            {
                return Result.NotFound("The catalog task was not found.");
            }

            if (RequestStateCalculator.IsFinal(task.State))
            {
                return Result.Invalid("Closed tasks cannot be changed.");
            }

            var previous = task.State;
            task.AssignmentGroupId = update.AssignmentGroupId;
            task.AssignedToId = update.AssignedToId;
            task.State = update.State;
            closed = RequestStateCalculator.IsFinal(update.State);
            if (closed)
            {
                task.Active = false;
                task.ClosedAt = clock.UtcNow;
                task.ClosedById = currentUser.UserId;
                task.CloseNotes = string.IsNullOrWhiteSpace(update.CloseNotes) ? null : update.CloseNotes.Trim();
            }
            else if (update.State == CatalogTaskState.WorkInProgress && task.AssignedToId is null)
            {
                task.AssignedToId = currentUser.UserId;
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Conflict("The task was changed by someone else. Reload and try again.");
            }

            if (previous != task.State)
            {
                await activity.AddSystemAsync(TableNames.CatalogTask, taskId, $"State changed from {previous} to {task.State}.", cancellationToken);
            }
        }

        if (!string.IsNullOrWhiteSpace(update.WorkNote))
        {
            await activity.AddAsync(TableNames.CatalogTask, taskId, ActivityType.WorkNote, update.WorkNote, cancellationToken);
        }

        if (closed)
        {
            await activity.AddSystemAsync(TableNames.RequestItem, task.RequestItemId, $"Catalog task {task.Number} closed ({task.State}).", cancellationToken);
            await fulfillment.RecomputeItemAsync(task.RequestItemId, cancellationToken);
        }

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> AssignTaskToMeAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Requests.Write, cancellationToken))
        {
            return Result.Forbidden();
        }

        if (currentUser.UserId is not { } me)
        {
            return Result.Invalid("Only users can be assigned to tasks.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var task = await db.Set<CatalogTask>().FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task is null)
        {
            return Result.NotFound("The catalog task was not found.");
        }

        if (RequestStateCalculator.IsFinal(task.State))
        {
            return Result.Invalid("Closed tasks cannot be assigned.");
        }

        task.AssignedToId = me;
        if (task.State == CatalogTaskState.Open)
        {
            task.State = CatalogTaskState.WorkInProgress;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> CancelRequestAsync(Guid requestId, string reason, CancellationToken cancellationToken = default)
    {
        reason = reason?.Trim() ?? string.Empty;
        if (reason.Length is 0 or > 1000)
        {
            return Result.Invalid("Please give a reason (max. 1000 characters).");
        }

        var canWrite = await currentUser.HasPermissionAsync(Permissions.Requests.Write, cancellationToken);
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var request = await db.Set<Request>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
            if (request is null)
            {
                return canWrite ? Result.NotFound("The request was not found.") : Result.Forbidden();
            }

            var own = currentUser.UserId is { } me && (request.RequestedForId == me || request.OpenedById == me);
            if (!canWrite && !own)
            {
                return Result.Forbidden();
            }

            if (RequestStateCalculator.IsFinal(request.State))
            {
                return Result.Invalid("The request is already closed.");
            }
        }

        var cancelled = await fulfillment.CancelRequestAsync(requestId, reason, cancellationToken);
        foreach (var itemId in cancelled)
        {
            await approvals.CancelAsync(TableNames.RequestItem, itemId, cancellationToken);
        }

        return Result.Success();
    }

    private static Dictionary<string, string?> ParseAnswers(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string?>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? new Dictionary<string, string?>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string?>();
        }
    }

    private static List<VariableAnswer> BuildAnswers(IReadOnlyList<CatalogVariable> definitions, IReadOnlyDictionary<string, string?> answers, IReadOnlyDictionary<Guid, string> users)
    {
        var result = new List<VariableAnswer>();
        foreach (var definition in definitions)
        {
            answers.TryGetValue(definition.Name, out var value);
            result.Add(new VariableAnswer(definition.Name, definition.Label, definition.Type, Display(definition.Type, value, users)));
        }

        foreach (var (name, value) in answers.Where(a => definitions.All(d => d.Name != a.Key)))
        {
            result.Add(new VariableAnswer(name, name, CatalogVariableType.Text, value));
        }

        return result;
    }

    private static string? Display(CatalogVariableType type, string? value, IReadOnlyDictionary<Guid, string> users)
    {
        if (value is null)
        {
            return null;
        }

        return type switch
        {
            CatalogVariableType.Boolean => value == "true" ? "Yes" : "No",
            CatalogVariableType.UserReference => Guid.TryParse(value, out var id) && users.TryGetValue(id, out var name) ? name : "Unknown user",
            CatalogVariableType.Date => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : value,
            _ => value,
        };
    }

    private static async Task<PagedResult<RequestListItem>> PageRequestsAsync(IAppDbContext db, IQueryable<Request> query, PageRequest page, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim().ToLower();
            query = query.Where(r => r.Number.ToLower().Contains(term) || r.ShortDescription.ToLower().Contains(term));
        }

        query = (page.SortBy, page.SortDescending) switch
        {
            ("State", false) => query.OrderBy(r => r.State).ThenByDescending(r => r.Number),
            ("State", true) => query.OrderByDescending(r => r.State).ThenByDescending(r => r.Number),
            ("Opened", false) => query.OrderBy(r => r.OpenedAt),
            ("Number", false) => query.OrderBy(r => r.Number),
            _ => query.OrderByDescending(r => r.Number),
        };

        var items = db.Set<RequestItem>();
        var projected = query.Select(r => new { Request = r, ItemCount = items.Count(i => i.RequestId == r.Id) });
        var paged = await projected.ToPagedResultAsync(page, cancellationToken);
        var users = await ReferenceNameResolver.UsersAsync(db, paged.Items.Select(r => r.Request.RequestedForId), cancellationToken);
        var rows = paged.Items.Select(r => new RequestListItem
        {
            Id = r.Request.Id,
            Number = r.Request.Number,
            ShortDescription = r.Request.ShortDescription,
            RequestedForName = users.NameOf(r.Request.RequestedForId),
            State = r.Request.State,
            ItemCount = r.ItemCount,
            TotalPrice = r.Request.TotalPrice,
            OpenedAt = r.Request.OpenedAt,
            DueAt = r.Request.DueAt,
        }).ToList();
        return new PagedResult<RequestListItem>(rows, paged.TotalCount, paged.Page, paged.PageSize);
    }

    private static async Task<List<CatalogTaskListItem>> MapTasksAsync(IAppDbContext db, IReadOnlyList<CatalogTask> tasks, CancellationToken cancellationToken)
    {
        var users = await ReferenceNameResolver.UsersAsync(db, tasks.Select(t => t.AssignedToId), cancellationToken);
        var groups = await ReferenceNameResolver.GroupsAsync(db, tasks.Select(t => t.AssignmentGroupId), cancellationToken);
        var itemIds = tasks.Select(t => t.RequestItemId).Distinct().ToList();
        var numbers = itemIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Set<RequestItem>().AsNoTracking().Where(r => itemIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Number, cancellationToken);
        return tasks.Select(t => new CatalogTaskListItem
        {
            Id = t.Id,
            Number = t.Number,
            RequestId = t.RequestId,
            RequestItemId = t.RequestItemId,
            RequestItemNumber = numbers.GetValueOrDefault(t.RequestItemId),
            ShortDescription = t.ShortDescription,
            State = t.State,
            Priority = t.Priority,
            AssignmentGroupId = t.AssignmentGroupId,
            AssignmentGroupName = groups.NameOf(t.AssignmentGroupId),
            AssignedToId = t.AssignedToId,
            AssignedToName = users.NameOf(t.AssignedToId),
            OpenedAt = t.OpenedAt,
            DueAt = t.DueAt,
            ClosedAt = t.ClosedAt,
            CloseNotes = t.CloseNotes,
        }).ToList();
    }
}
