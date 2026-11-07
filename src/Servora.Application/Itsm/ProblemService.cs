using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Directory;
using Servora.Application.Platform;
using Servora.Application.Security;
using Servora.Domain.Activity;
using Servora.Domain.Common;
using Servora.Domain.Identity;
using Servora.Domain.Itsm;

namespace Servora.Application.Itsm;

/// <summary>Default <see cref="IProblemService"/> implementation.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="numbers">The number sequence service.</param>
/// <param name="lookup">The directory lookup service (display names).</param>
/// <param name="clock">The clock.</param>
public sealed class ProblemService(
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    INumberSequenceService numbers,
    IDirectoryLookupService lookup,
    IClock clock) : IProblemService
{
    private const string Table = TableNames.Problem;
    private const int MaxBulk = 200;
    private const int MaxLink = 100;

    /// <inheritdoc />
    public async Task<Result<PagedResult<ProblemListItem>>> ListAsync(ProblemQuickFilter filter, PageRequest request, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Problems.Read, cancellationToken))
        {
            return Result<PagedResult<ProblemListItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = ApplyFilter(db.Set<Problem>().AsNoTracking(), filter);
        query = ApplySearch(db, query, request.Search);
        var total = await query.CountAsync(cancellationToken);
        var page = await ApplySort(query, request.SortBy, request.SortDescending)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
        var items = await ToListItemsAsync(db, page, cancellationToken);
        return Result<PagedResult<ProblemListItem>>.Success(new PagedResult<ProblemListItem>(items, total, request.Page, request.PageSize));
    }

    /// <inheritdoc />
    public async Task<Result<ProblemStats>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Problems.Read, cancellationToken))
        {
            return Result<ProblemStats>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var problems = db.Set<Problem>().AsNoTracking();
        var open = OpenOnly(problems);
        var openCount = await open.CountAsync(cancellationToken);
        var high = await open.CountAsync(p => p.Priority == Priority.Critical || p.Priority == Priority.High, cancellationToken);
        var unassigned = await open.CountAsync(p => p.AssignedToId == null, cancellationToken);
        var knownErrors = await problems.CountAsync(p => p.Active && p.IsKnownError, cancellationToken);
        var activeProblemIds = problems.Where(p => p.Active).Select(p => p.Id);
        var linked = await db.Set<Incident>().AsNoTracking().CountAsync(
            i => i.ProblemId != null && activeProblemIds.Contains(i.ProblemId.Value)
                && (i.State == IncidentState.New || i.State == IncidentState.InProgress || i.State == IncidentState.OnHold),
            cancellationToken);
        return Result<ProblemStats>.Success(new ProblemStats(openCount, high, unassigned, knownErrors, linked));
    }

    /// <inheritdoc />
    public async Task<Result<ProblemDetails>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Problems.Read, cancellationToken))
        {
            return Result<ProblemDetails>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var problem = await db.Set<Problem>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (problem is null)
        {
            return Result<ProblemDetails>.NotFound("The problem was not found.");
        }

        ItsmPerson? assignee = null;
        if (problem.AssignedToId is { } assigneeId)
        {
            assignee = await db.Set<User>().AsNoTracking().Where(u => u.Id == assigneeId)
                .Select(u => new ItsmPerson(u.Id, u.DisplayName, u.Email, u.Phone, u.Title, u.Department, u.Location))
                .FirstOrDefaultAsync(cancellationToken);
        }

        var users = await lookup.GetUsersAsync(new[] { problem.OpenedById, problem.ResolvedById }.Where(u => u.HasValue).Select(u => u!.Value), cancellationToken);
        var groups = await lookup.GetGroupsAsync(problem.AssignmentGroupId is { } g ? [g] : [], cancellationToken);
        var cis = await lookup.GetConfigurationItemsAsync(new[] { problem.ConfigurationItemId, problem.BusinessServiceId }.Where(c => c.HasValue).Select(c => c!.Value), cancellationToken);
        var incidents = db.Set<Incident>().AsNoTracking().Where(i => i.ProblemId == id);
        var incidentCount = await incidents.CountAsync(cancellationToken);
        var openIncidents = await incidents.CountAsync(i => i.State == IncidentState.New || i.State == IncidentState.InProgress || i.State == IncidentState.OnHold, cancellationToken);

        return Result<ProblemDetails>.Success(new ProblemDetails
        {
            Id = problem.Id,
            Number = problem.Number,
            Version = problem.Version,
            State = problem.State,
            Priority = problem.Priority,
            Fields = ToInput(problem),
            AssignedTo = assignee,
            AssignmentGroupName = problem.AssignmentGroupId is { } gid ? groups.GetValueOrDefault(gid)?.Display : null,
            ConfigurationItemName = problem.ConfigurationItemId is { } ci ? cis.GetValueOrDefault(ci)?.Display : null,
            BusinessServiceName = problem.BusinessServiceId is { } bs ? cis.GetValueOrDefault(bs)?.Display : null,
            ResolutionCode = problem.ResolutionCode,
            IncidentCount = incidentCount,
            OpenIncidentCount = openIncidents,
            OpenedAt = problem.OpenedAt,
            OpenedByName = Name(users, problem.OpenedById),
            ResolvedAt = problem.ResolvedAt,
            ResolvedByName = Name(users, problem.ResolvedById),
            ClosedAt = problem.ClosedAt,
            UpdatedAt = problem.UpdatedAt,
            UpdatedBy = problem.UpdatedBy,
        });
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<LookupItem>>> SearchAsync(string? term, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Problems.Read, cancellationToken))
        {
            return Result<IReadOnlyList<LookupItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<Problem>().AsNoTracking().Where(p => p.Active);
        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim().ToLower();
            query = query.Where(p => p.Number.ToLower().Contains(t) || p.ShortDescription.ToLower().Contains(t));
        }

        var items = await query.OrderByDescending(p => p.OpenedAt).Take(10)
            .Select(p => new LookupItem(p.Id, p.Number, p.ShortDescription)).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<LookupItem>>.Success(items);
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateAsync(ProblemInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!await currentUser.HasPermissionAsync(Permissions.Problems.Create, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        if (input.Validate() is { } validationError)
        {
            return Result<Guid>.Invalid(validationError);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await ItsmData.ValidateReferencesAsync(db, [input.AssignedToId], input.AssignmentGroupId, [input.ConfigurationItemId, input.BusinessServiceId], cancellationToken) is { } referenceError)
        {
            return Result<Guid>.Invalid(referenceError);
        }

        var now = clock.UtcNow;
        var problem = new Problem
        {
            Number = await numbers.NextAsync(Table, Problem.NumberPrefix, cancellationToken),
            State = ProblemState.New,
            OpenedAt = now,
            OpenedById = currentUser.UserId,
        };
        Apply(problem, input);
        db.Set<Problem>().Add(problem);
        db.Set<ActivityEntry>().Add(ItsmActivity.System(Table, problem.Id, $"Problem {problem.Number} opened.", currentUser, now));
        var result = await ItsmData.SaveAsync(db, "problem", cancellationToken);
        return result.Succeeded ? Result<Guid>.Success(problem.Id) : Result<Guid>.From(result);
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateFromIncidentAsync(Guid incidentId, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Problems.Create, cancellationToken)
            || !await currentUser.HasPermissionAsync(Permissions.Incidents.Write, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var incident = await db.Set<Incident>().FirstOrDefaultAsync(i => i.Id == incidentId, cancellationToken);
        if (incident is null)
        {
            return Result<Guid>.NotFound("The incident was not found.");
        }

        if (incident.ProblemId is not null)
        {
            return Result<Guid>.Conflict("The incident is already linked to a problem.");
        }

        var now = clock.UtcNow;
        var problem = new Problem
        {
            Number = await numbers.NextAsync(Table, Problem.NumberPrefix, cancellationToken),
            State = ProblemState.New,
            OpenedAt = now,
            OpenedById = currentUser.UserId,
            ShortDescription = incident.ShortDescription,
            Description = incident.Description,
            Category = incident.Category,
            Impact = incident.Impact,
            Urgency = incident.Urgency,
            AssignmentGroupId = incident.AssignmentGroupId,
            ConfigurationItemId = incident.ConfigurationItemId,
            BusinessServiceId = incident.BusinessServiceId,
        };
        db.Set<Problem>().Add(problem);
        incident.ProblemId = problem.Id;
        db.Set<ActivityEntry>().Add(ItsmActivity.System(Table, problem.Id, $"Problem {problem.Number} created from incident {incident.Number}.", currentUser, now));
        db.Set<ActivityEntry>().Add(ItsmActivity.System(TableNames.Incident, incident.Id, $"Problem {problem.Number} created and linked.", currentUser, now));
        var result = await ItsmData.SaveAsync(db, "problem", cancellationToken);
        return result.Succeeded ? Result<Guid>.Success(problem.Id) : Result<Guid>.From(result);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync(Guid id, long version, ProblemInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!await currentUser.HasPermissionAsync(Permissions.Problems.Write, cancellationToken))
        {
            return Result.Forbidden();
        }

        if (input.Validate() is { } validationError)
        {
            return Result.Invalid(validationError);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var problem = await db.Set<Problem>().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (problem is null)
        {
            return Result.NotFound("The problem was not found.");
        }

        if (ProblemStateMachine.IsFinal(problem.State))
        {
            return Result.Invalid($"{ItsmLabels.For(problem.State)} problems cannot be edited.");
        }

        if (problem.Version != version)
        {
            return Result.Conflict("The problem was changed by someone else in the meantime. Reload the record and apply your changes again.");
        }

        if (await ItsmData.ValidateReferencesAsync(db, [input.AssignedToId], input.AssignmentGroupId, [input.ConfigurationItemId, input.BusinessServiceId], cancellationToken) is { } referenceError)
        {
            return Result.Invalid(referenceError);
        }

        var before = ToInput(problem);
        var oldPriority = problem.Priority;
        Apply(problem, input);
        var changes = await DescribeChangesAsync(before, ToInput(problem), oldPriority, PriorityMatrix.Calculate(problem.Impact, problem.Urgency), cancellationToken);
        if (ItsmActivity.FieldChanges(Table, problem.Id, changes, currentUser, clock.UtcNow) is { } entry)
        {
            db.Set<ActivityEntry>().Add(entry);
        }

        return await ItsmData.SaveAsync(db, "problem", cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> MoveToAsync(Guid id, ProblemState target, CancellationToken cancellationToken = default) =>
        MutateAsync(id, (db, problem, now) =>
        {
            if (target is not (ProblemState.Assess or ProblemState.RootCauseAnalysis or ProblemState.FixInProgress))
            {
                return "Use the resolve, close or cancel actions for final states.";
            }

            if (problem.State == target || !ProblemStateMachine.CanTransition(problem.State, target))
            {
                return $"A problem cannot move from {ItsmLabels.For(problem.State)} to {ItsmLabels.For(target)}.";
            }

            var from = problem.State;
            problem.State = target;
            if (target == ProblemState.FixInProgress && from == ProblemState.Resolved)
            {
                problem.ResolutionCode = null;
            }

            db.Set<ActivityEntry>().Add(ItsmActivity.System(Table, problem.Id, $"State changed from {ItsmLabels.For(from)} to {ItsmLabels.For(target)}.", currentUser, now));
            return null;
        }, cancellationToken);

    /// <inheritdoc />
    public Task<Result> ResolveAsync(Guid id, ProblemResolutionCode code, string fixNotes, CancellationToken cancellationToken = default) =>
        MutateAsync(id, (db, problem, now) =>
        {
            if (!Enum.IsDefined(code))
            {
                return "Select a valid resolution code.";
            }

            var text = ItsmData.Clean(fixNotes);
            if (text is null)
            {
                return "Fix notes are required to resolve a problem.";
            }

            if (text.Length > ProblemInput.AnalysisMaxLength)
            {
                return $"Fix notes must not exceed {ProblemInput.AnalysisMaxLength:N0} characters.";
            }

            if (problem.State == ProblemState.Resolved || !ProblemStateMachine.CanTransition(problem.State, ProblemState.Resolved))
            {
                return $"A problem in state {ItsmLabels.For(problem.State)} cannot be resolved. Complete the root cause analysis first.";
            }

            problem.State = ProblemState.Resolved;
            problem.ResolutionCode = code;
            problem.FixNotes = text;
            problem.ResolvedAt = now;
            problem.ResolvedById = currentUser.UserId;
            db.Set<ActivityEntry>().Add(ItsmActivity.System(Table, problem.Id, $"Resolved – {ItsmLabels.For(code)}.\n{text}", currentUser, now));
            return null;
        }, cancellationToken);

    /// <inheritdoc />
    public Task<Result> CloseAsync(Guid id, CancellationToken cancellationToken = default) =>
        MutateAsync(id, (db, problem, now) =>
        {
            if (problem.State != ProblemState.Resolved)
            {
                return "Only resolved problems can be closed.";
            }

            problem.State = ProblemState.Closed;
            problem.ClosedAt = now;
            problem.ClosedById = currentUser.UserId;
            db.Set<ActivityEntry>().Add(ItsmActivity.System(Table, problem.Id, "Problem closed.", currentUser, now));
            return null;
        }, cancellationToken);

    /// <inheritdoc />
    public Task<Result> ReopenAsync(Guid id, string reason, CancellationToken cancellationToken = default) =>
        MutateAsync(id, (db, problem, now) =>
        {
            var text = ItsmData.Clean(reason);
            if (text is null)
            {
                return "Please explain why the problem is reopened.";
            }

            if (problem.State != ProblemState.Resolved)
            {
                return "Only resolved problems can be reopened.";
            }

            problem.State = ProblemState.FixInProgress;
            problem.ResolutionCode = null;
            db.Set<ActivityEntry>().Add(ItsmActivity.System(Table, problem.Id, $"Problem reopened: {Truncate(text)}", currentUser, now));
            return null;
        }, cancellationToken);

    /// <inheritdoc />
    public Task<Result> CancelAsync(Guid id, string reason, CancellationToken cancellationToken = default) =>
        MutateAsync(id, (db, problem, now) =>
        {
            var text = ItsmData.Clean(reason);
            if (text is null)
            {
                return "Please explain why the problem is cancelled.";
            }

            if (!ProblemStateMachine.IsOpen(problem.State))
            {
                return $"A problem in state {ItsmLabels.For(problem.State)} cannot be cancelled.";
            }

            problem.State = ProblemState.Cancelled;
            problem.ClosedAt = now;
            problem.ClosedById = currentUser.UserId;
            db.Set<ActivityEntry>().Add(ItsmActivity.System(Table, problem.Id, $"Problem cancelled: {Truncate(text)}", currentUser, now));
            return null;
        }, cancellationToken);

    /// <inheritdoc />
    public Task<Result> AssignToMeAsync(Guid id, CancellationToken cancellationToken = default) =>
        MutateAsync(id, (db, problem, now) =>
        {
            if (currentUser.UserId is not { } me)
            {
                return "Only users can assign records to themselves.";
            }

            if (!problem.Active)
            {
                return "Closed or cancelled problems cannot be reassigned.";
            }

            if (problem.AssignedToId == me)
            {
                return "The problem is already assigned to you.";
            }

            problem.AssignedToId = me;
            db.Set<ActivityEntry>().Add(ItsmActivity.System(Table, problem.Id, $"Assigned to {currentUser.DisplayName}.", currentUser, now));
            return null;
        }, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<int>> AssignAsync(IReadOnlyCollection<Guid> ids, Guid? groupId, Guid? userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (!await currentUser.HasPermissionAsync(Permissions.Problems.Write, cancellationToken))
        {
            return Result<int>.Forbidden();
        }

        var set = ids.Distinct().ToList();
        if (set.Count == 0)
        {
            return Result<int>.Invalid("Select at least one problem.");
        }

        if (set.Count > MaxBulk)
        {
            return Result<int>.Invalid($"At most {MaxBulk} problems can be assigned at once.");
        }

        if (groupId is null && userId is null)
        {
            return Result<int>.Invalid("Select an assignment group, an assignee or both.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await ItsmData.ValidateReferencesAsync(db, [userId], groupId, [], cancellationToken) is { } referenceError)
        {
            return Result<int>.Invalid(referenceError);
        }

        var problems = await db.Set<Problem>().Where(p => set.Contains(p.Id) && p.Active).ToListAsync(cancellationToken);
        var users = await lookup.GetUsersAsync(problems.Select(p => p.AssignedToId).Append(userId).Where(u => u.HasValue).Select(u => u!.Value), cancellationToken);
        var groups = await lookup.GetGroupsAsync(problems.Select(p => p.AssignmentGroupId).Append(groupId).Where(g => g.HasValue).Select(g => g!.Value), cancellationToken);
        var now = clock.UtcNow;
        foreach (var problem in problems)
        {
            var changes = new List<(string Field, string? From, string? To)>();
            if (groupId is { } newGroup && problem.AssignmentGroupId != newGroup)
            {
                ItsmActivity.Track(changes, "Assignment group", Display(groups, problem.AssignmentGroupId), Display(groups, newGroup));
                problem.AssignmentGroupId = newGroup;
            }

            if (userId is { } newUser && problem.AssignedToId != newUser)
            {
                ItsmActivity.Track(changes, "Assigned to", Display(users, problem.AssignedToId), Display(users, newUser));
                problem.AssignedToId = newUser;
            }

            if (ItsmActivity.FieldChanges(Table, problem.Id, changes, currentUser, now) is { } entry)
            {
                db.Set<ActivityEntry>().Add(entry);
            }
        }

        var result = await ItsmData.SaveAsync(db, "problem", cancellationToken);
        return result.Succeeded ? Result<int>.Success(problems.Count) : Result<int>.From(result);
    }

    /// <inheritdoc />
    public async Task<Result<int>> LinkIncidentsAsync(Guid id, IReadOnlyCollection<Guid> incidentIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(incidentIds);
        if (!await currentUser.HasPermissionAsync(Permissions.Problems.Write, cancellationToken)
            || !await currentUser.HasPermissionAsync(Permissions.Incidents.Write, cancellationToken))
        {
            return Result<int>.Forbidden();
        }

        var set = incidentIds.Distinct().ToList();
        if (set.Count == 0)
        {
            return Result<int>.Invalid("Select at least one incident.");
        }

        if (set.Count > MaxLink)
        {
            return Result<int>.Invalid($"At most {MaxLink} incidents can be linked at once.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var problem = await db.Set<Problem>().AsNoTracking().Where(p => p.Id == id).Select(p => new { p.Number, p.Active }).FirstOrDefaultAsync(cancellationToken);
        if (problem is null)
        {
            return Result<int>.NotFound("The problem was not found.");
        }

        if (!problem.Active)
        {
            return Result<int>.Invalid("Incidents can only be linked to active problems.");
        }

        var incidents = await db.Set<Incident>().Where(i => set.Contains(i.Id) && i.ProblemId != id).ToListAsync(cancellationToken);
        var now = clock.UtcNow;
        foreach (var incident in incidents)
        {
            incident.ProblemId = id;
            db.Set<ActivityEntry>().Add(ItsmActivity.System(TableNames.Incident, incident.Id, $"Linked to problem {problem.Number}.", currentUser, now));
            db.Set<ActivityEntry>().Add(ItsmActivity.System(Table, id, $"Incident {incident.Number} linked.", currentUser, now));
        }

        var result = await ItsmData.SaveAsync(db, "incident", cancellationToken);
        return result.Succeeded ? Result<int>.Success(incidents.Count) : Result<int>.From(result);
    }

    /// <inheritdoc />
    public async Task<Result> UnlinkIncidentAsync(Guid id, Guid incidentId, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Problems.Write, cancellationToken)
            || !await currentUser.HasPermissionAsync(Permissions.Incidents.Write, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var incident = await db.Set<Incident>().FirstOrDefaultAsync(i => i.Id == incidentId && i.ProblemId == id, cancellationToken);
        if (incident is null)
        {
            return Result.NotFound("The incident is not linked to this problem.");
        }

        var number = await db.Set<Problem>().Where(p => p.Id == id).Select(p => p.Number).FirstOrDefaultAsync(cancellationToken);
        var now = clock.UtcNow;
        incident.ProblemId = null;
        db.Set<ActivityEntry>().Add(ItsmActivity.System(TableNames.Incident, incident.Id, $"Unlinked from problem {number}.", currentUser, now));
        db.Set<ActivityEntry>().Add(ItsmActivity.System(Table, id, $"Incident {incident.Number} unlinked.", currentUser, now));
        return await ItsmData.SaveAsync(db, "incident", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Problems.Delete, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var problem = await db.Set<Problem>().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (problem is null)
        {
            return Result.NotFound("The problem was not found.");
        }

        var linked = await db.Set<Incident>().Where(i => i.ProblemId == id).Take(MaxBulk * 5).ToListAsync(cancellationToken);
        foreach (var incident in linked)
        {
            incident.ProblemId = null;
        }

        db.Set<Problem>().Remove(problem);
        var result = await ItsmData.SaveAsync(db, "problem", cancellationToken);
        if (result.Succeeded)
        {
            await db.Set<ActivityEntry>().Where(a => a.TableName == Table && a.RecordId == id).ExecuteDeleteAsync(cancellationToken);
        }

        return result;
    }

    /// <summary>Applies the editable input fields to an entity.</summary>
    /// <param name="problem">The entity.</param>
    /// <param name="input">The input.</param>
    internal static void Apply(Problem problem, ProblemInput input)
    {
        problem.ShortDescription = input.ShortDescription.Trim();
        problem.Description = ItsmData.Clean(input.Description);
        problem.Category = ItsmData.Clean(input.Category);
        problem.Impact = input.Impact;
        problem.Urgency = input.Urgency;
        problem.AssignmentGroupId = input.AssignmentGroupId;
        problem.AssignedToId = input.AssignedToId;
        problem.ConfigurationItemId = input.ConfigurationItemId;
        problem.BusinessServiceId = input.BusinessServiceId;
        problem.IsKnownError = input.IsKnownError;
        problem.Workaround = ItsmData.Clean(input.Workaround);
        problem.RootCause = ItsmData.Clean(input.RootCause);
        problem.FixNotes = ItsmData.Clean(input.FixNotes);
        problem.Tags = ItsmData.Clean(input.Tags);
    }

    /// <summary>Copies the editable fields of an entity into an input object.</summary>
    /// <param name="problem">The entity.</param>
    /// <returns>The input.</returns>
    internal static ProblemInput ToInput(Problem problem) => new()
    {
        ShortDescription = problem.ShortDescription,
        Description = problem.Description,
        Category = problem.Category,
        Impact = problem.Impact,
        Urgency = problem.Urgency,
        AssignmentGroupId = problem.AssignmentGroupId,
        AssignedToId = problem.AssignedToId,
        ConfigurationItemId = problem.ConfigurationItemId,
        BusinessServiceId = problem.BusinessServiceId,
        IsKnownError = problem.IsKnownError,
        Workaround = problem.Workaround,
        RootCause = problem.RootCause,
        FixNotes = problem.FixNotes,
        Tags = problem.Tags,
    };

    private static IQueryable<Problem> OpenOnly(IQueryable<Problem> query) =>
        query.Where(p => p.State == ProblemState.New || p.State == ProblemState.Assess || p.State == ProblemState.RootCauseAnalysis || p.State == ProblemState.FixInProgress);

    private static string? Name(IReadOnlyDictionary<Guid, LookupItem> items, Guid? id) =>
        id is { } key ? items.GetValueOrDefault(key)?.Display : null;

    private static string? Display(IReadOnlyDictionary<Guid, LookupItem> items, Guid? id) => Name(items, id) ?? (id is null ? null : "(unknown)");

    private static string Truncate(string text) => text.Length > 500 ? text[..497] + "…" : text;

    private IQueryable<Problem> ApplyFilter(IQueryable<Problem> query, ProblemQuickFilter filter)
    {
        var me = currentUser.UserId ?? Guid.Empty;
        return filter switch
        {
            ProblemQuickFilter.MyOpen => OpenOnly(query).Where(p => p.AssignedToId == me),
            ProblemQuickFilter.Unassigned => OpenOnly(query).Where(p => p.AssignedToId == null),
            ProblemQuickFilter.HighPriority => OpenOnly(query).Where(p => p.Priority == Priority.Critical || p.Priority == Priority.High),
            ProblemQuickFilter.KnownErrors => query.Where(p => p.Active && p.IsKnownError),
            ProblemQuickFilter.Resolved => query.Where(p => p.State == ProblemState.Resolved),
            ProblemQuickFilter.All => query,
            _ => query.Where(p => p.Active),
        };
    }

    private static IQueryable<Problem> ApplySearch(IAppDbContext db, IQueryable<Problem> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var t = search.Trim().ToLower();
        var people = db.Set<User>().Where(u => u.DisplayName.ToLower().Contains(t)).Select(u => u.Id);
        return query.Where(p => p.Number.ToLower().Contains(t)
            || p.ShortDescription.ToLower().Contains(t)
            || (p.Category != null && p.Category.ToLower().Contains(t))
            || (p.AssignedToId != null && people.Contains(p.AssignedToId.Value)));
    }

    private static IQueryable<Problem> ApplySort(IQueryable<Problem> query, string? sortBy, bool descending)
    {
        IOrderedQueryable<Problem> ordered = (sortBy ?? "Priority") switch
        {
            "Number" => descending ? query.OrderByDescending(p => p.Number) : query.OrderBy(p => p.Number),
            "State" => descending ? query.OrderByDescending(p => p.State) : query.OrderBy(p => p.State),
            "ShortDescription" => descending ? query.OrderByDescending(p => p.ShortDescription) : query.OrderBy(p => p.ShortDescription),
            "Category" => descending ? query.OrderByDescending(p => p.Category) : query.OrderBy(p => p.Category),
            "OpenedAt" => descending ? query.OrderByDescending(p => p.OpenedAt) : query.OrderBy(p => p.OpenedAt),
            "UpdatedAt" => descending ? query.OrderByDescending(p => p.UpdatedAt) : query.OrderBy(p => p.UpdatedAt),
            _ => descending
                ? query.OrderByDescending(p => p.Priority).ThenByDescending(p => p.UpdatedAt)
                : query.OrderBy(p => p.Priority).ThenByDescending(p => p.UpdatedAt),
        };
        return ordered.ThenByDescending(p => p.Number);
    }

    private async Task<IReadOnlyList<ProblemListItem>> ToListItemsAsync(IAppDbContext db, IReadOnlyList<Problem> rows, CancellationToken cancellationToken)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var counts = ids.Count == 0
            ? new Dictionary<Guid, int>()
            : await db.Set<Incident>().AsNoTracking().Where(i => i.ProblemId != null && ids.Contains(i.ProblemId.Value))
                .GroupBy(i => i.ProblemId!.Value).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var users = await lookup.GetUsersAsync(rows.Where(r => r.AssignedToId.HasValue).Select(r => r.AssignedToId!.Value), cancellationToken);
        var groups = await lookup.GetGroupsAsync(rows.Where(r => r.AssignmentGroupId.HasValue).Select(r => r.AssignmentGroupId!.Value), cancellationToken);
        return rows.Select(r => new ProblemListItem(
            r.Id,
            r.Number,
            r.ShortDescription,
            r.State,
            r.Priority,
            r.IsKnownError,
            r.Category,
            r.AssignedToId,
            Name(users, r.AssignedToId),
            Name(groups, r.AssignmentGroupId),
            counts.GetValueOrDefault(r.Id),
            r.OpenedAt,
            r.UpdatedAt)).ToList();
    }

    private async Task<List<(string Field, string? From, string? To)>> DescribeChangesAsync(
        ProblemInput before, ProblemInput after, Priority oldPriority, Priority newPriority, CancellationToken cancellationToken)
    {
        var changes = new List<(string Field, string? From, string? To)>();
        var users = await lookup.GetUsersAsync(new[] { before.AssignedToId, after.AssignedToId }.Where(u => u.HasValue).Select(u => u!.Value), cancellationToken);
        var groups = await lookup.GetGroupsAsync(new[] { before.AssignmentGroupId, after.AssignmentGroupId }.Where(g => g.HasValue).Select(g => g!.Value), cancellationToken);
        var cis = await lookup.GetConfigurationItemsAsync(new[] { before.ConfigurationItemId, after.ConfigurationItemId, before.BusinessServiceId, after.BusinessServiceId }.Where(c => c.HasValue).Select(c => c!.Value), cancellationToken);

        ItsmActivity.Track(changes, "Short description", before.ShortDescription, after.ShortDescription);
        TrackText(changes, "Description", before.Description, after.Description);
        ItsmActivity.Track(changes, "Category", before.Category, after.Category);
        ItsmActivity.Track(changes, "Impact", ItsmLabels.For(before.Impact), ItsmLabels.For(after.Impact));
        ItsmActivity.Track(changes, "Urgency", ItsmLabels.For(before.Urgency), ItsmLabels.For(after.Urgency));
        ItsmActivity.Track(changes, "Priority", ItsmLabels.For(oldPriority), ItsmLabels.For(newPriority));
        ItsmActivity.Track(changes, "Assignment group", Display(groups, before.AssignmentGroupId), Display(groups, after.AssignmentGroupId));
        ItsmActivity.Track(changes, "Assigned to", Display(users, before.AssignedToId), Display(users, after.AssignedToId));
        ItsmActivity.Track(changes, "Configuration item", Display(cis, before.ConfigurationItemId), Display(cis, after.ConfigurationItemId));
        ItsmActivity.Track(changes, "Business service", Display(cis, before.BusinessServiceId), Display(cis, after.BusinessServiceId));
        ItsmActivity.Track(changes, "Known error", before.IsKnownError ? "Yes" : "No", after.IsKnownError ? "Yes" : "No");
        TrackText(changes, "Workaround", before.Workaround, after.Workaround);
        TrackText(changes, "Root cause", before.RootCause, after.RootCause);
        TrackText(changes, "Fix notes", before.FixNotes, after.FixNotes);
        ItsmActivity.Track(changes, "Tags", before.Tags, after.Tags);
        return changes;
    }

    private static void TrackText(List<(string Field, string? From, string? To)> changes, string field, string? before, string? after)
    {
        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            changes.Add((field, before is null ? null : "(previous text)", after is null ? null : "(updated)"));
        }
    }

    private async Task<Result> MutateAsync(Guid id, Func<IAppDbContext, Problem, DateTimeOffset, string?> mutate, CancellationToken cancellationToken)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Problems.Write, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var problem = await db.Set<Problem>().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (problem is null)
        {
            return Result.NotFound("The problem was not found.");
        }

        if (mutate(db, problem, clock.UtcNow) is { } error)
        {
            return Result.Invalid(error);
        }

        return await ItsmData.SaveAsync(db, "problem", cancellationToken);
    }
}
