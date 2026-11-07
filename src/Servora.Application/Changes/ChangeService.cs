using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Activity;
using Servora.Application.Approvals;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Platform;
using Servora.Application.Security;
using Servora.Domain.Activity;
using Servora.Domain.Approvals;
using Servora.Domain.Changes;
using Servora.Domain.Common;
using Servora.Domain.Identity;
using Servora.Domain.Itsm;

namespace Servora.Application.Changes;

/// <summary>Default <see cref="IChangeService"/> implementation.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="clock">The clock.</param>
/// <param name="numbers">The number sequence service.</param>
/// <param name="activity">The activity stream service.</param>
/// <param name="approvals">The approval engine.</param>
/// <param name="audit">The audit service.</param>
public sealed class ChangeService(
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    IClock clock,
    INumberSequenceService numbers,
    IActivityService activity,
    ApprovalEngine approvals,
    IAuditService audit) : IChangeService
{
    private const int MaxCalendarEntries = 500;
    private const int MaxConflicts = 50;
    private const int MaxTasks = 200;

    /// <inheritdoc />
    public async Task<Result<PagedResult<ChangeListItem>>> ListAsync(ChangeListFilter filter, PageRequest page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!await currentUser.HasPermissionAsync(Permissions.Changes.Read, cancellationToken))
        {
            return Result<PagedResult<ChangeListItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<ChangeRequest>().AsNoTracking();
        var me = currentUser.UserId;
        query = filter switch
        {
            ChangeListFilter.Open => query.Where(c => c.State != ChangeState.Closed && c.State != ChangeState.Cancelled),
            ChangeListFilter.AssignedToMe => query.Where(c => c.AssignedToId == me && c.State != ChangeState.Closed && c.State != ChangeState.Cancelled),
            ChangeListFilter.AwaitingApproval => query.Where(c => c.State == ChangeState.Authorize),
            ChangeListFilter.Scheduled => query.Where(c => c.State == ChangeState.Scheduled || c.State == ChangeState.Implement),
            ChangeListFilter.Emergency => query.Where(c => c.Type == ChangeType.Emergency && c.State != ChangeState.Closed && c.State != ChangeState.Cancelled),
            ChangeListFilter.Closed => query.Where(c => c.State == ChangeState.Closed || c.State == ChangeState.Cancelled),
            _ => query,
        };

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim().ToLower();
            query = query.Where(c => c.Number.ToLower().Contains(term) || c.ShortDescription.ToLower().Contains(term));
        }

        query = (page.SortBy, page.SortDescending) switch
        {
            ("State", false) => query.OrderBy(c => c.State).ThenByDescending(c => c.Number),
            ("State", true) => query.OrderByDescending(c => c.State).ThenByDescending(c => c.Number),
            ("PlannedStart", false) => query.OrderBy(c => c.PlannedStartAt),
            ("PlannedStart", true) => query.OrderByDescending(c => c.PlannedStartAt),
            ("Priority", false) => query.OrderBy(c => c.Priority).ThenByDescending(c => c.Number),
            ("Priority", true) => query.OrderByDescending(c => c.Priority).ThenByDescending(c => c.Number),
            ("Updated", false) => query.OrderBy(c => c.UpdatedAt),
            ("Updated", true) => query.OrderByDescending(c => c.UpdatedAt),
            ("Number", false) => query.OrderBy(c => c.Number),
            _ => query.OrderByDescending(c => c.Number),
        };

        var paged = await query.ToPagedResultAsync(page, cancellationToken);
        var users = await ReferenceNameResolver.UsersAsync(db, paged.Items.Select(c => c.AssignedToId), cancellationToken);
        var groups = await ReferenceNameResolver.GroupsAsync(db, paged.Items.Select(c => c.AssignmentGroupId), cancellationToken);
        var cis = await ReferenceNameResolver.ConfigurationItemsAsync(db, paged.Items.Select(c => c.ConfigurationItemId), cancellationToken);
        var items = paged.Items.Select(c => new ChangeListItem
        {
            Id = c.Id,
            Number = c.Number,
            ShortDescription = c.ShortDescription,
            Type = c.Type,
            State = c.State,
            Risk = c.Risk,
            Priority = c.Priority,
            Approval = c.Approval,
            AssignmentGroupName = groups.NameOf(c.AssignmentGroupId),
            AssignedToName = users.NameOf(c.AssignedToId),
            ConfigurationItemName = cis.NameOf(c.ConfigurationItemId),
            PlannedStartAt = c.PlannedStartAt,
            PlannedEndAt = c.PlannedEndAt,
            UpdatedAt = c.UpdatedAt,
        }).ToList();
        return Result<PagedResult<ChangeListItem>>.Success(new PagedResult<ChangeListItem>(items, paged.TotalCount, paged.Page, paged.PageSize));
    }

    /// <inheritdoc />
    public async Task<Result<ChangeStats>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Changes.Read, cancellationToken))
        {
            return Result<ChangeStats>.Forbidden();
        }

        var now = clock.UtcNow;
        var nextWeek = now.AddDays(7);
        var since = now.AddDays(-90);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var changes = db.Set<ChangeRequest>().AsNoTracking();
        var open = await changes.CountAsync(c => c.State != ChangeState.Closed && c.State != ChangeState.Cancelled, cancellationToken);
        var awaiting = await changes.CountAsync(c => c.State == ChangeState.Authorize, cancellationToken);
        var scheduled = await changes.CountAsync(c => c.State == ChangeState.Scheduled && c.PlannedStartAt >= now && c.PlannedStartAt < nextWeek, cancellationToken);
        var implementing = await changes.CountAsync(c => c.State == ChangeState.Implement, cancellationToken);
        var emergency = await changes.CountAsync(c => c.Type == ChangeType.Emergency && c.State != ChangeState.Closed && c.State != ChangeState.Cancelled, cancellationToken);
        var codes = await changes.Where(c => c.State == ChangeState.Closed && c.ClosedAt >= since).Select(c => c.CloseCode).Take(5000).ToListAsync(cancellationToken);
        double? rate = codes.Count == 0 ? null : Math.Round(100.0 * codes.Count(c => c != ChangeCloseCode.Unsuccessful) / codes.Count, 1);
        return Result<ChangeStats>.Success(new ChangeStats(open, awaiting, scheduled, implementing, emergency, rate));
    }

    /// <inheritdoc />
    public async Task<Result<ChangeDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Changes.Read, cancellationToken))
        {
            return Result<ChangeDetail>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var change = await db.Set<ChangeRequest>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (change is null)
        {
            return Result<ChangeDetail>.NotFound("The change request was not found.");
        }

        var tasks = await db.Set<ChangeTask>().AsNoTracking()
            .Where(t => t.ChangeRequestId == id)
            .OrderBy(t => t.Order).ThenBy(t => t.Number)
            .Take(MaxTasks)
            .ToListAsync(cancellationToken);
        var cabId = change.ApprovalGroupId ?? await FindCabGroupAsync(db, cancellationToken);
        var users = await ReferenceNameResolver.UsersAsync(db, [change.AssignedToId, change.RequestedById, change.OpenedById, .. tasks.Select(t => t.AssignedToId)], cancellationToken);
        var groups = await ReferenceNameResolver.GroupsAsync(db, [change.AssignmentGroupId, cabId, .. tasks.Select(t => t.AssignmentGroupId)], cancellationToken);
        var cis = await ReferenceNameResolver.ConfigurationItemsAsync(db, [change.ConfigurationItemId], cancellationToken);

        IReadOnlyList<ChangeConflict> conflicts = [];
        if (!ChangeStateMachine.IsFinal(change.State) && change is { ConfigurationItemId: { } ci, PlannedStartAt: { } start, PlannedEndAt: { } end } && end > start)
        {
            conflicts = await QueryConflictsAsync(db, ci, start, end, change.Id, cancellationToken);
        }

        return Result<ChangeDetail>.Success(new ChangeDetail
        {
            Id = change.Id,
            Number = change.Number,
            ShortDescription = change.ShortDescription,
            Description = change.Description,
            Type = change.Type,
            State = change.State,
            Category = change.Category,
            Impact = change.Impact,
            Urgency = change.Urgency,
            Priority = change.Priority,
            Risk = change.Risk,
            RiskScore = change.RiskScore,
            RiskAssessedAt = change.RiskAssessedAt,
            RiskAnswers = ParseAnswers(change.RiskAssessmentJson),
            AssignmentGroupId = change.AssignmentGroupId,
            AssignmentGroupName = groups.NameOf(change.AssignmentGroupId),
            AssignedToId = change.AssignedToId,
            AssignedToName = users.NameOf(change.AssignedToId),
            RequestedById = change.RequestedById,
            RequestedByName = users.NameOf(change.RequestedById),
            ConfigurationItemId = change.ConfigurationItemId,
            ConfigurationItemName = cis.NameOf(change.ConfigurationItemId),
            ApprovalGroupId = change.ApprovalGroupId,
            ApprovalGroupName = groups.NameOf(cabId),
            Approval = change.Approval,
            PlannedStartAt = change.PlannedStartAt,
            PlannedEndAt = change.PlannedEndAt,
            ActualStartAt = change.ActualStartAt,
            ActualEndAt = change.ActualEndAt,
            Justification = change.Justification,
            ImplementationPlan = change.ImplementationPlan,
            BackoutPlan = change.BackoutPlan,
            TestPlan = change.TestPlan,
            CloseCode = change.CloseCode,
            CloseNotes = change.CloseNotes,
            OpenedAt = change.OpenedAt,
            OpenedByName = users.NameOf(change.OpenedById),
            ClosedAt = change.ClosedAt,
            UpdatedAt = change.UpdatedAt,
            Version = change.Version,
            Tasks = tasks.Select(t => new ChangeTaskItem
            {
                Id = t.Id,
                Number = t.Number,
                ShortDescription = t.ShortDescription,
                Description = t.Description,
                State = t.State,
                AssignmentGroupId = t.AssignmentGroupId,
                AssignmentGroupName = groups.NameOf(t.AssignmentGroupId),
                AssignedToId = t.AssignedToId,
                AssignedToName = users.NameOf(t.AssignedToId),
                PlannedStartAt = t.PlannedStartAt,
                PlannedEndAt = t.PlannedEndAt,
                CloseNotes = t.CloseNotes,
            }).ToList(),
            Conflicts = conflicts,
            NextStates = ChangeStateMachine.GetNextStates(change.Type, change.State),
        });
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateAsync(ChangeEditModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!await currentUser.HasPermissionAsync(Permissions.Changes.Create, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        if (Validate(model) is { } error)
        {
            return Result<Guid>.Invalid(error);
        }

        var change = new ChangeRequest
        {
            Number = await numbers.NextAsync(TableNames.ChangeRequest, ChangeTables.ChangeRequestPrefix, cancellationToken),
            State = ChangeState.New,
            OpenedAt = clock.UtcNow,
            OpenedById = currentUser.UserId,
            RequestedById = model.RequestedById ?? currentUser.UserId,
        };
        Apply(change, model, allowTypeChange: true);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.Set<ChangeRequest>().Add(change);
        await db.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(change.Id);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync(Guid id, ChangeEditModel model, long expectedVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!await currentUser.HasPermissionAsync(Permissions.Changes.Write, cancellationToken))
        {
            return Result.Forbidden();
        }

        if (Validate(model) is { } error)
        {
            return Result.Invalid(error);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var change = await db.Set<ChangeRequest>().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (change is null)
        {
            return Result.NotFound("The change request was not found.");
        }

        if (change.Version != expectedVersion)
        {
            return Result.Conflict("The change was modified by someone else. Reload it and apply your changes again.");
        }

        if (ChangeStateMachine.IsFinal(change.State))
        {
            return Result.Invalid("Closed and cancelled changes cannot be edited.");
        }

        if (change.State == ChangeState.Authorize && (change.PlannedStartAt != Utc(model.PlannedStartAt) || change.PlannedEndAt != Utc(model.PlannedEndAt)))
        {
            return Result.Invalid("The planned window cannot be changed while the change is being authorized. Move it back to Assess first.");
        }

        Apply(change, model, allowTypeChange: change.State == ChangeState.New);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict("The change was modified by someone else. Reload it and apply your changes again.");
        }

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<RiskAssessmentResult>> AssessRiskAsync(Guid id, IReadOnlyDictionary<string, int> answers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(answers);
        if (!await currentUser.HasPermissionAsync(Permissions.Changes.Write, cancellationToken))
        {
            return Result<RiskAssessmentResult>.Forbidden();
        }

        var result = ChangeRiskCalculator.Calculate(answers);
        if (result is null)
        {
            return Result<RiskAssessmentResult>.Invalid("Answer every question of the risk assessment.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var change = await db.Set<ChangeRequest>().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (change is null)
        {
            return Result<RiskAssessmentResult>.NotFound("The change request was not found.");
        }

        if (change.State is not (ChangeState.New or ChangeState.Assess))
        {
            return Result<RiskAssessmentResult>.Invalid("The risk can only be assessed while the change is new or being assessed.");
        }

        var stored = ChangeRiskCalculator.Questions.ToDictionary(q => q.Key, q => answers[q.Key]);
        change.RiskAssessmentJson = JsonSerializer.Serialize(stored);
        change.RiskScore = result.Score;
        change.Risk = result.Risk;
        change.RiskAssessedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await activity.AddSystemAsync(TableNames.ChangeRequest, id, $"Risk assessed: {result.Risk} (score {result.Score}).", cancellationToken);
        return Result<RiskAssessmentResult>.Success(result);
    }

    /// <inheritdoc />
    public async Task<Result> TransitionAsync(Guid id, ChangeState target, ChangeTransitionInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!await currentUser.HasPermissionAsync(Permissions.Changes.Write, cancellationToken))
        {
            return Result.Forbidden();
        }

        if (input.Note is { Length: > 4000 } || input.CloseNotes is { Length: > 4000 })
        {
            return Result.Invalid("Notes must not exceed 4000 characters.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var change = await db.Set<ChangeRequest>().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (change is null)
        {
            return Result.NotFound("The change request was not found.");
        }

        var tasks = await db.Set<ChangeTask>().Where(t => t.ChangeRequestId == id).Take(MaxTasks).ToListAsync(cancellationToken);
        var context = new ChangeTransitionContext
        {
            Type = change.Type,
            Current = change.State,
            Target = target,
            PlannedStartAt = change.PlannedStartAt,
            PlannedEndAt = change.PlannedEndAt,
            HasImplementationPlan = !string.IsNullOrWhiteSpace(change.ImplementationPlan),
            HasBackoutPlan = !string.IsNullOrWhiteSpace(change.BackoutPlan),
            RiskAssessed = change.RiskAssessedAt.HasValue,
            Approval = change.Approval,
            OpenTaskCount = tasks.Count(t => t.State is ChangeTaskState.Open or ChangeTaskState.InProgress),
            CloseCode = input.CloseCode,
            CloseNotes = input.CloseNotes,
        };
        if (ChangeStateMachine.Validate(context) is { } error)
        {
            return Result.Invalid(error);
        }

        var previous = change.State;
        var now = clock.UtcNow;
        var requestedApproval = false;
        if (target == ChangeState.Authorize)
        {
            var groupId = change.ApprovalGroupId ?? await FindCabGroupAsync(db, cancellationToken);
            if (groupId is null)
            {
                return Result.Invalid($"No approval group is configured. Create the group \"{ChangeTables.ChangeAdvisoryBoardGroup}\" or select an approval group.");
            }

            var policy = change.Type == ChangeType.Normal && change.Risk >= ChangeRisk.High ? ApprovalPolicy.AllApprovers : ApprovalPolicy.AnyApprover;
            var reason = $"{change.Type} change · risk {change.Risk} · planned {change.PlannedStartAt:yyyy-MM-dd HH:mm} – {change.PlannedEndAt:yyyy-MM-dd HH:mm} UTC";
            var requested = await approvals.RequestAsync(new ApprovalRequest(TableNames.ChangeRequest, change.Id, [], [groupId.Value], policy, reason), cancellationToken);
            if (!requested.Succeeded)
            {
                return requested;
            }

            requestedApproval = true;
            change.Approval = ApprovalStatus.Requested;
        }
        else if (previous == ChangeState.Authorize && target != ChangeState.Scheduled)
        {
            await approvals.CancelAsync(TableNames.ChangeRequest, change.Id, cancellationToken);
            if (change.Approval == ApprovalStatus.Requested)
            {
                change.Approval = ApprovalStatus.Cancelled;
            }
        }

        switch (target)
        {
            case ChangeState.Implement:
                change.ActualStartAt ??= now;
                break;
            case ChangeState.Review:
                change.ActualEndAt ??= now;
                break;
            case ChangeState.Closed:
                change.CloseCode = input.CloseCode;
                change.CloseNotes = input.CloseNotes!.Trim();
                change.ClosedAt = now;
                change.ClosedById = currentUser.UserId;
                change.Active = false;
                break;
            case ChangeState.Cancelled:
                change.ClosedAt = now;
                change.ClosedById = currentUser.UserId;
                change.Active = false;
                foreach (var task in tasks.Where(t => t.State is ChangeTaskState.Open or ChangeTaskState.InProgress))
                {
                    task.State = ChangeTaskState.Cancelled;
                    task.Active = false;
                    task.ClosedAt = now;
                    task.ClosedById = currentUser.UserId;
                }

                break;
        }

        change.State = target;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (requestedApproval)
            {
                await approvals.CancelAsync(TableNames.ChangeRequest, change.Id, cancellationToken);
            }

            return Result.Conflict("The change was modified by someone else. Reload it and try again.");
        }

        await activity.AddSystemAsync(TableNames.ChangeRequest, id, $"State changed from {previous} to {target}.", cancellationToken);
        if (!string.IsNullOrWhiteSpace(input.Note))
        {
            await activity.AddAsync(TableNames.ChangeRequest, id, ActivityType.WorkNote, input.Note, cancellationToken);
        }

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ChangeConflict>>> FindConflictsAsync(Guid configurationItemId, DateTimeOffset start, DateTimeOffset end, Guid? excludeChangeId, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Changes.Read, cancellationToken))
        {
            return Result<IReadOnlyList<ChangeConflict>>.Forbidden();
        }

        if (end <= start)
        {
            return Result<IReadOnlyList<ChangeConflict>>.Success([]);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return Result<IReadOnlyList<ChangeConflict>>.Success(await QueryConflictsAsync(db, configurationItemId, start.ToUniversalTime(), end.ToUniversalTime(), excludeChangeId, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ChangeCalendarEntry>>> GetCalendarAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Changes.Read, cancellationToken))
        {
            return Result<IReadOnlyList<ChangeCalendarEntry>>.Forbidden();
        }

        if (to <= from || (to - from).TotalDays > 62)
        {
            return Result<IReadOnlyList<ChangeCalendarEntry>>.Invalid("The calendar period must be between one day and two months.");
        }

        from = from.ToUniversalTime();
        to = to.ToUniversalTime();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Set<ChangeRequest>().AsNoTracking()
            .Where(c => c.State != ChangeState.Cancelled && c.PlannedStartAt != null && c.PlannedEndAt != null && c.PlannedStartAt < to && c.PlannedEndAt > from)
            .OrderBy(c => c.PlannedStartAt)
            .Take(MaxCalendarEntries)
            .Select(c => new { c.Id, c.Number, c.ShortDescription, c.Type, c.State, c.Risk, c.PlannedStartAt, c.PlannedEndAt, c.ConfigurationItemId })
            .ToListAsync(cancellationToken);
        var cis = await ReferenceNameResolver.ConfigurationItemsAsync(db, rows.Select(r => r.ConfigurationItemId), cancellationToken);
        IReadOnlyList<ChangeCalendarEntry> entries = rows
            .Select(r => new ChangeCalendarEntry(r.Id, r.Number, r.ShortDescription, r.Type, r.State, r.Risk, r.PlannedStartAt!.Value, r.PlannedEndAt!.Value, cis.NameOf(r.ConfigurationItemId)))
            .ToList();
        return Result<IReadOnlyList<ChangeCalendarEntry>>.Success(entries);
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> AddTaskAsync(Guid changeId, ChangeTaskEditModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!await currentUser.HasPermissionAsync(Permissions.Changes.Write, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        if (ValidateTask(model) is { } error)
        {
            return Result<Guid>.Invalid(error);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var change = await db.Set<ChangeRequest>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == changeId, cancellationToken);
        if (change is null)
        {
            return Result<Guid>.NotFound("The change request was not found.");
        }

        if (ChangeStateMachine.IsFinal(change.State))
        {
            return Result<Guid>.Invalid("Tasks cannot be added to closed or cancelled changes.");
        }

        var count = await db.Set<ChangeTask>().CountAsync(t => t.ChangeRequestId == changeId, cancellationToken);
        if (count >= MaxTasks)
        {
            return Result<Guid>.Invalid($"A change can have at most {MaxTasks} tasks.");
        }

        var task = new ChangeTask
        {
            Number = await numbers.NextAsync(ChangeTables.ChangeTask, ChangeTables.ChangeTaskPrefix, cancellationToken),
            ChangeRequestId = changeId,
            OpenedAt = clock.UtcNow,
            OpenedById = currentUser.UserId,
            Priority = change.Priority,
            Impact = change.Impact,
            Urgency = change.Urgency,
            ConfigurationItemId = change.ConfigurationItemId,
            Order = (count + 1) * 10,
        };
        ApplyTask(task, model);
        db.Set<ChangeTask>().Add(task);
        await db.SaveChangesAsync(cancellationToken);
        await activity.AddSystemAsync(TableNames.ChangeRequest, changeId, $"Change task {task.Number} added: {task.ShortDescription}", cancellationToken);
        return Result<Guid>.Success(task.Id);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateTaskAsync(Guid taskId, ChangeTaskEditModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!await currentUser.HasPermissionAsync(Permissions.Changes.Write, cancellationToken))
        {
            return Result.Forbidden();
        }

        if (ValidateTask(model) is { } error)
        {
            return Result.Invalid(error);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var task = await db.Set<ChangeTask>().FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task is null)
        {
            return Result.NotFound("The change task was not found.");
        }

        if (!task.Active)
        {
            return Result.Invalid("Completed and cancelled tasks cannot be edited.");
        }

        ApplyTask(task, model);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> SetTaskStateAsync(Guid taskId, ChangeTaskState state, string? closeNotes, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Changes.Write, cancellationToken))
        {
            return Result.Forbidden();
        }

        if (!Enum.IsDefined(state))
        {
            return Result.Invalid("The task state is invalid.");
        }

        if (closeNotes is { Length: > 4000 })
        {
            return Result.Invalid("Close notes must not exceed 4000 characters.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var task = await db.Set<ChangeTask>().FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task is null)
        {
            return Result.NotFound("The change task was not found.");
        }

        if (task.State is ChangeTaskState.Completed or ChangeTaskState.Cancelled)
        {
            return Result.Invalid("Completed and cancelled tasks cannot be reopened.");
        }

        var previous = task.State;
        task.State = state;
        if (state is ChangeTaskState.Completed or ChangeTaskState.Cancelled)
        {
            task.Active = false;
            task.ClosedAt = clock.UtcNow;
            task.ClosedById = currentUser.UserId;
            task.CloseNotes = string.IsNullOrWhiteSpace(closeNotes) ? null : closeNotes.Trim();
        }
        else if (state == ChangeTaskState.InProgress && task.AssignedToId is null)
        {
            task.AssignedToId = currentUser.UserId;
        }

        await db.SaveChangesAsync(cancellationToken);
        await activity.AddSystemAsync(TableNames.ChangeRequest, task.ChangeRequestId, $"Change task {task.Number}: {previous} → {state}.", cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Changes.Delete, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var change = await db.Set<ChangeRequest>().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (change is null)
        {
            return Result.NotFound("The change request was not found.");
        }

        if (change.State is not (ChangeState.New or ChangeState.Cancelled))
        {
            return Result.Invalid("Only new or cancelled changes can be deleted. Cancel the change first.");
        }

        var tasks = await db.Set<ChangeTask>().Where(t => t.ChangeRequestId == id).Take(MaxTasks).ToListAsync(cancellationToken);
        db.Set<ChangeTask>().RemoveRange(tasks);
        db.Set<ChangeRequest>().Remove(change);
        await db.SaveChangesAsync(cancellationToken);
        await approvals.CancelAsync(TableNames.ChangeRequest, id, cancellationToken);
        await audit.WriteAsync(new AuditEntry("change", "change.deleted") { TargetType = TableNames.ChangeRequest, TargetId = id.ToString(), TargetName = change.Number }, cancellationToken);
        return Result.Success();
    }

    private static async Task<Guid?> FindCabGroupAsync(IAppDbContext db, CancellationToken cancellationToken) =>
        await db.Set<Group>().AsNoTracking()
            .Where(g => g.Name == ChangeTables.ChangeAdvisoryBoardGroup && g.IsActive)
            .Select(g => (Guid?)g.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private static async Task<IReadOnlyList<ChangeConflict>> QueryConflictsAsync(IAppDbContext db, Guid ci, DateTimeOffset start, DateTimeOffset end, Guid? excludeId, CancellationToken cancellationToken)
    {
        var query = db.Set<ChangeRequest>().AsNoTracking()
            .Where(c => c.ConfigurationItemId == ci
                        && c.State != ChangeState.Closed && c.State != ChangeState.Cancelled
                        && c.PlannedStartAt != null && c.PlannedEndAt != null
                        && c.PlannedStartAt < end && c.PlannedEndAt > start);
        if (excludeId is { } exclude)
        {
            query = query.Where(c => c.Id != exclude);
        }

        return await query.OrderBy(c => c.PlannedStartAt)
            .Take(MaxConflicts)
            .Select(c => new ChangeConflict(c.Id, c.Number, c.ShortDescription, c.State, c.PlannedStartAt, c.PlannedEndAt))
            .ToListAsync(cancellationToken);
    }

    private static IReadOnlyDictionary<string, int> ParseAnswers(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, int>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, int>();
        }
    }

    private static DateTimeOffset? Utc(DateTimeOffset? value) => value?.ToUniversalTime();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Validate(ChangeEditModel model)
    {
        if (string.IsNullOrWhiteSpace(model.ShortDescription))
        {
            return "The short description is required.";
        }

        if (model.ShortDescription.Trim().Length > 300)
        {
            return "The short description must not exceed 300 characters.";
        }

        if (!Enum.IsDefined(model.Type) || !Enum.IsDefined(model.Impact) || !Enum.IsDefined(model.Urgency))
        {
            return "The type, impact or urgency is invalid.";
        }

        if (model.Description is { Length: > 32_000 } || model.Justification is { Length: > 8000 } || model.Category is { Length: > 100 })
        {
            return "A text field exceeds its maximum length.";
        }

        if (model.ImplementationPlan is { Length: > 16_000 } || model.BackoutPlan is { Length: > 16_000 } || model.TestPlan is { Length: > 16_000 })
        {
            return "Plans must not exceed 16,000 characters.";
        }

        if (model.PlannedStartAt is { } start && model.PlannedEndAt is { } end && end <= start)
        {
            return "The planned end must be after the planned start.";
        }

        return null;
    }

    private static void Apply(ChangeRequest change, ChangeEditModel model, bool allowTypeChange)
    {
        change.ShortDescription = model.ShortDescription.Trim();
        change.Description = Clean(model.Description);
        if (allowTypeChange)
        {
            change.Type = model.Type;
        }

        change.Category = Clean(model.Category);
        change.Impact = model.Impact;
        change.Urgency = model.Urgency;
        change.Priority = PriorityMatrix.Calculate(model.Impact, model.Urgency);
        change.AssignmentGroupId = model.AssignmentGroupId;
        change.AssignedToId = model.AssignedToId;
        change.RequestedById = model.RequestedById ?? change.RequestedById;
        change.ConfigurationItemId = model.ConfigurationItemId;
        change.ApprovalGroupId = model.ApprovalGroupId;
        change.PlannedStartAt = Utc(model.PlannedStartAt);
        change.PlannedEndAt = Utc(model.PlannedEndAt);
        change.DueAt = change.PlannedEndAt;
        change.Justification = Clean(model.Justification);
        change.ImplementationPlan = Clean(model.ImplementationPlan);
        change.BackoutPlan = Clean(model.BackoutPlan);
        change.TestPlan = Clean(model.TestPlan);
    }

    private static string? ValidateTask(ChangeTaskEditModel model)
    {
        if (string.IsNullOrWhiteSpace(model.ShortDescription) || model.ShortDescription.Trim().Length > 300)
        {
            return "The task needs a short description of at most 300 characters.";
        }

        if (model.Description is { Length: > 32_000 })
        {
            return "The description is too long.";
        }

        if (model.PlannedStartAt is { } start && model.PlannedEndAt is { } end && end <= start)
        {
            return "The planned end must be after the planned start.";
        }

        return null;
    }

    private static void ApplyTask(ChangeTask task, ChangeTaskEditModel model)
    {
        task.ShortDescription = model.ShortDescription.Trim();
        task.Description = Clean(model.Description);
        task.AssignmentGroupId = model.AssignmentGroupId;
        task.AssignedToId = model.AssignedToId;
        task.PlannedStartAt = Utc(model.PlannedStartAt);
        task.PlannedEndAt = Utc(model.PlannedEndAt);
    }
}
