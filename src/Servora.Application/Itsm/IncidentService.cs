using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Directory;
using Servora.Application.Platform;
using Servora.Application.Security;
using Servora.Application.Sla;
using Servora.Domain.Activity;
using Servora.Domain.Cmdb;
using Servora.Domain.Common;
using Servora.Domain.Identity;
using Servora.Domain.Itsm;

namespace Servora.Application.Itsm;

/// <summary>Default <see cref="IIncidentService"/> implementation.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="numbers">The number sequence service.</param>
/// <param name="lookup">The directory lookup service (display names).</param>
/// <param name="slaService">The SLA service (breach KPI).</param>
/// <param name="clock">The clock.</param>
public sealed class IncidentService(
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    INumberSequenceService numbers,
    IDirectoryLookupService lookup,
    ISlaService slaService,
    IClock clock) : IIncidentService
{
    private const string Table = TableNames.Incident;
    private const int MaxBulk = 200;
    private const int SlaSampleSize = 100;
    private const int MaxNotesLength = 8000;

    /// <inheritdoc />
    public async Task<Result<PagedResult<IncidentListItem>>> ListAsync(IncidentQuickFilter filter, PageRequest request, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Incidents.Read, cancellationToken))
        {
            return Result<PagedResult<IncidentListItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = await ApplyFilterAsync(db, db.Set<Incident>().AsNoTracking(), filter, cancellationToken);
        query = ApplySearch(db, query, request.Search);
        var total = await query.CountAsync(cancellationToken);
        var page = await ApplySort(query, request.SortBy, request.SortDescending)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
        var items = await ToListItemsAsync(page, cancellationToken);
        return Result<PagedResult<IncidentListItem>>.Success(new PagedResult<IncidentListItem>(items, total, request.Page, request.PageSize));
    }

    /// <inheritdoc />
    public async Task<Result<IncidentStats>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Incidents.Read, cancellationToken))
        {
            return Result<IncidentStats>.Forbidden();
        }

        var now = clock.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var incidents = db.Set<Incident>().AsNoTracking();
        var open = incidents.Where(i => i.State == IncidentState.New || i.State == IncidentState.InProgress || i.State == IncidentState.OnHold);
        var openCount = await open.CountAsync(cancellationToken);
        var critical = await open.CountAsync(i => i.Priority == Priority.Critical, cancellationToken);
        var unassigned = await open.CountAsync(i => i.AssignedToId == null, cancellationToken);
        var major = await open.CountAsync(i => i.IsMajorIncident, cancellationToken);
        var since24 = now.AddHours(-24);
        var resolved24 = await incidents.CountAsync(i => i.ResolvedAt != null && i.ResolvedAt >= since24, cancellationToken);

        var start = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddDays(-6);
        var opened = await incidents.Where(i => i.OpenedAt >= start).Select(i => i.OpenedAt).Take(20_000).ToListAsync(cancellationToken);
        var resolved = await incidents.Where(i => i.ResolvedAt != null && i.ResolvedAt >= start).Select(i => i.ResolvedAt!.Value).Take(20_000).ToListAsync(cancellationToken);

        // ISlaService only exposes per-record timers, so the breach KPI samples the most recently updated active incidents.
        var sample = await incidents.Where(i => i.Active).OrderByDescending(i => i.UpdatedAt).Select(i => i.Id).Take(SlaSampleSize).ToListAsync(cancellationToken);
        var breached = 0;
        foreach (var id in sample)
        {
            var timers = await slaService.GetForRecordAsync(Table, id, cancellationToken);
            if (timers.Any(t => t.Breached))
            {
                breached++;
            }
        }

        return Result<IncidentStats>.Success(new IncidentStats(
            openCount, critical, unassigned, breached, major, resolved24, Bucket(opened, start, 7), Bucket(resolved, start, 7)));
    }

    /// <inheritdoc />
    public async Task<Result<IncidentDetails>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Incidents.Read, cancellationToken))
        {
            return Result<IncidentDetails>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var incident = await db.Set<Incident>().AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (incident is null)
        {
            return Result<IncidentDetails>.NotFound("The incident was not found.");
        }

        ItsmPerson? caller = null;
        if (incident.CallerId is { } callerId)
        {
            caller = await db.Set<User>().AsNoTracking().Where(u => u.Id == callerId)
                .Select(u => new ItsmPerson(u.Id, u.DisplayName, u.Email, u.Phone, u.Title, u.Department, u.Location))
                .FirstOrDefaultAsync(cancellationToken);
        }

        var users = await lookup.GetUsersAsync(new[] { incident.AssignedToId, incident.OpenedById, incident.ResolvedById }.Where(u => u.HasValue).Select(u => u!.Value), cancellationToken);
        var groups = await lookup.GetGroupsAsync(incident.AssignmentGroupId is { } g ? [g] : [], cancellationToken);
        var ciIds = new[] { incident.ConfigurationItemId, incident.BusinessServiceId }.Where(c => c.HasValue).Select(c => c!.Value).ToList();
        var cis = await db.Set<ConfigurationItem>().AsNoTracking().Where(c => ciIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.ClassName }).ToListAsync(cancellationToken);

        string? parentNumber = null;
        if (incident.ParentIncidentId is { } parentId)
        {
            parentNumber = await db.Set<Incident>().AsNoTracking().Where(i => i.Id == parentId).Select(i => i.Number).FirstOrDefaultAsync(cancellationToken);
        }

        var problem = incident.ProblemId is { } problemId
            ? await db.Set<Problem>().AsNoTracking().Where(p => p.Id == problemId)
                .Select(p => new { p.Number, p.ShortDescription, p.State }).FirstOrDefaultAsync(cancellationToken)
            : null;
        var childCount = await db.Set<Incident>().CountAsync(i => i.ParentIncidentId == id, cancellationToken);
        var ci = cis.FirstOrDefault(c => c.Id == incident.ConfigurationItemId);

        return Result<IncidentDetails>.Success(new IncidentDetails
        {
            Id = incident.Id,
            Number = incident.Number,
            Version = incident.Version,
            State = incident.State,
            Priority = incident.Priority,
            Fields = ToInput(incident),
            Caller = caller,
            AssignedToName = Name(users, incident.AssignedToId),
            AssignmentGroupName = incident.AssignmentGroupId is { } gid ? groups.GetValueOrDefault(gid)?.Display : null,
            ConfigurationItemName = ci?.Name,
            ConfigurationItemClass = ci?.ClassName,
            BusinessServiceName = cis.FirstOrDefault(c => c.Id == incident.BusinessServiceId)?.Name,
            ParentIncidentNumber = parentNumber,
            ProblemId = problem is null ? null : incident.ProblemId,
            ProblemNumber = problem?.Number,
            ProblemShortDescription = problem?.ShortDescription,
            ProblemState = problem?.State,
            OnHoldReason = incident.OnHoldReason,
            ResolutionCode = incident.ResolutionCode,
            ResolutionNotes = incident.ResolutionNotes,
            ReopenCount = incident.ReopenCount,
            ReassignmentCount = incident.ReassignmentCount,
            ChildCount = childCount,
            OpenedAt = incident.OpenedAt,
            OpenedByName = Name(users, incident.OpenedById),
            ResolvedAt = incident.ResolvedAt,
            ResolvedByName = Name(users, incident.ResolvedById),
            ClosedAt = incident.ClosedAt,
            UpdatedAt = incident.UpdatedAt,
            UpdatedBy = incident.UpdatedBy,
        });
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CategoryOption>>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Incidents.Read, cancellationToken)
            && !await currentUser.HasPermissionAsync(Permissions.Problems.Read, cancellationToken))
        {
            return Result<IReadOnlyList<CategoryOption>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var all = await db.Set<IncidentCategory>().AsNoTracking().Where(c => c.IsActive).Take(1000).ToListAsync(cancellationToken);
        var options = all.Where(c => c.ParentId is null)
            .OrderBy(c => c.Order).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => new CategoryOption(c.Name, all.Where(s => s.ParentId == c.Id)
                .OrderBy(s => s.Order).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase).Select(s => s.Name).ToList()))
            .ToList();
        return Result<IReadOnlyList<CategoryOption>>.Success(options);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<LookupItem>>> SearchAsync(string? term, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Incidents.Read, cancellationToken))
        {
            return Result<IReadOnlyList<LookupItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<Incident>().AsNoTracking();
        if (excludeId is { } exclude)
        {
            query = query.Where(i => i.Id != exclude);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim().ToLower();
            query = query.Where(i => i.Number.ToLower().Contains(t) || i.ShortDescription.ToLower().Contains(t));
        }

        var items = await query.OrderByDescending(i => i.Active).ThenByDescending(i => i.OpenedAt).Take(10)
            .Select(i => new LookupItem(i.Id, i.Number, i.ShortDescription)).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<LookupItem>>.Success(items);
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateAsync(IncidentInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!await currentUser.HasPermissionAsync(Permissions.Incidents.Create, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        if (input.Validate() is { } validationError)
        {
            return Result<Guid>.Invalid(validationError);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var callerId = input.CallerId ?? currentUser.UserId;
        if (await ItsmData.ValidateReferencesAsync(db, [callerId, input.AssignedToId], input.AssignmentGroupId, [input.ConfigurationItemId, input.BusinessServiceId], cancellationToken) is { } referenceError)
        {
            return Result<Guid>.Invalid(referenceError);
        }

        if (input.ParentIncidentId is { } parentId && !await db.Set<Incident>().AnyAsync(i => i.Id == parentId, cancellationToken))
        {
            return Result<Guid>.Invalid("The parent incident does not exist.");
        }

        var now = clock.UtcNow;
        var incident = new Incident
        {
            Number = await numbers.NextAsync(Table, Incident.NumberPrefix, cancellationToken),
            State = IncidentState.New,
            OpenedAt = now,
            OpenedById = currentUser.UserId,
            CallerId = callerId,
        };
        Apply(incident, input);
        db.Set<Incident>().Add(incident);
        db.Set<ActivityEntry>().Add(ItsmActivity.System(Table, incident.Id, $"Incident {incident.Number} opened via {ItsmLabels.For(incident.Channel)}.", currentUser, now));
        var result = await ItsmData.SaveAsync(db, "incident", cancellationToken);
        return result.Succeeded ? Result<Guid>.Success(incident.Id) : Result<Guid>.From(result);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync(Guid id, long version, IncidentInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!await currentUser.HasPermissionAsync(Permissions.Incidents.Write, cancellationToken))
        {
            return Result.Forbidden();
        }

        if (input.Validate() is { } validationError)
        {
            return Result.Invalid(validationError);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var incident = await db.Set<Incident>().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (incident is null)
        {
            return Result.NotFound("The incident was not found.");
        }

        if (IncidentStateMachine.IsFinal(incident.State))
        {
            return Result.Invalid($"{ItsmLabels.For(incident.State)} incidents cannot be edited.");
        }

        if (await ItsmData.ValidateReferencesAsync(db, [input.CallerId, input.AssignedToId], input.AssignmentGroupId, [input.ConfigurationItemId, input.BusinessServiceId], cancellationToken) is { } referenceError)
        {
            return Result.Invalid(referenceError);
        }

        if (input.ParentIncidentId is { } parentId)
        {
            if (parentId == id)
            {
                return Result.Invalid("An incident cannot be its own parent.");
            }

            var parent = await db.Set<Incident>().AsNoTracking().Where(i => i.Id == parentId).Select(i => new { i.ParentIncidentId }).FirstOrDefaultAsync(cancellationToken);
            if (parent is null)
            {
                return Result.Invalid("The parent incident does not exist.");
            }

            if (parent.ParentIncidentId == id)
            {
                return Result.Invalid("The selected parent is already a child of this incident.");
            }
        }

        // The loaded version is the concurrency token of the update; comparing it with the version the user edited
        // detects changes made since the form was opened.
        if (incident.Version != version)
        {
            return Result.Conflict("The incident was changed by someone else in the meantime. Reload the record and apply your changes again.");
        }

        var before = ToInput(incident);
        var oldPriority = incident.Priority;
        if (before.AssignmentGroupId is not null && before.AssignmentGroupId != input.AssignmentGroupId)
        {
            incident.ReassignmentCount++;
        }

        Apply(incident, input);
        var changes = await DescribeChangesAsync(db, before, ToInput(incident), oldPriority, PriorityMatrix.Calculate(incident.Impact, incident.Urgency), cancellationToken);
        if (ItsmActivity.FieldChanges(Table, incident.Id, changes, currentUser, clock.UtcNow) is { } entry)
        {
            db.Set<ActivityEntry>().Add(entry);
        }

        return await ItsmData.SaveAsync(db, "incident", cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> StartWorkAsync(Guid id, CancellationToken cancellationToken = default) =>
        MutateAsync(id, Permissions.Incidents.Write, (db, incident, now) =>
        {
            if (incident.State is not (IncidentState.New or IncidentState.OnHold))
            {
                return $"Work can only be started on new or on-hold incidents (current state: {ItsmLabels.For(incident.State)}).";
            }

            var from = incident.State;
            incident.State = IncidentState.InProgress;
            if (incident.AssignedToId is null && currentUser.UserId is { } me)
            {
                incident.AssignedToId = me;
            }

            db.Set<ActivityEntry>().Add(ItsmActivity.System(Table, incident.Id, $"State changed from {ItsmLabels.For(from)} to In Progress.", currentUser, now));
            return null;
        }, cancellationToken);

    /// <inheritdoc />
    public Task<Result> PutOnHoldAsync(Guid id, OnHoldReason reason, string? note, CancellationToken cancellationToken = default) =>
        MutateAsync(id, Permissions.Incidents.Write, (db, incident, now) =>
        {
            if (!Enum.IsDefined(reason))
            {
                return "Select a valid on-hold reason.";
            }

            if (!IncidentStateMachine.CanTransition(incident.State, IncidentState.OnHold) || incident.State == IncidentState.OnHold)
            {
                return $"An incident in state {ItsmLabels.For(incident.State)} cannot be put on hold.";
            }

            if (note?.Length > MaxNotesLength)
            {
                return $"The note must not exceed {MaxNotesLength:N0} characters.";
            }

            incident.State = IncidentState.OnHold;
            incident.OnHoldReason = reason;
            db.Set<ActivityEntry>().Add(ItsmActivity.System(Table, incident.Id, $"Put on hold: {ItsmLabels.For(reason)}.", currentUser, now));
            if (ItsmData.Clean(note) is { } text)
            {
                db.Set<ActivityEntry>().Add(ItsmActivity.WorkNote(Table, incident.Id, text, currentUser, now));
            }

            return null;
        }, cancellationToken);

    /// <inheritdoc />
    public Task<Result> ResolveAsync(Guid id, IncidentResolutionCode code, string notes, CancellationToken cancellationToken = default) =>
        MutateAsync(id, Permissions.Incidents.Write, (db, incident, now) =>
        {
            if (!Enum.IsDefined(code))
            {
                return "Select a valid resolution code.";
            }

            var text = ItsmData.Clean(notes);
            if (text is null)
            {
                return "Resolution notes are required to resolve an incident.";
            }

            if (text.Length > MaxNotesLength)
            {
                return $"Resolution notes must not exceed {MaxNotesLength:N0} characters.";
            }

            if (!IncidentStateMachine.CanTransition(incident.State, IncidentState.Resolved) || incident.State == IncidentState.Resolved)
            {
                return $"An incident in state {ItsmLabels.For(incident.State)} cannot be resolved.";
            }

            incident.State = IncidentState.Resolved;
            incident.ResolutionCode = code;
            incident.ResolutionNotes = text;
            incident.ResolvedAt = now;
            incident.ResolvedById = currentUser.UserId;
            db.Set<ActivityEntry>().Add(ItsmActivity.System(Table, incident.Id, $"Resolved – {ItsmLabels.For(code)}.\n{text}", currentUser, now));
            return null;
        }, cancellationToken);

    /// <inheritdoc />
    public Task<Result> CloseAsync(Guid id, CancellationToken cancellationToken = default) =>
        MutateAsync(id, Permissions.Incidents.Write, (db, incident, now) =>
        {
            if (incident.State != IncidentState.Resolved)
            {
                return "Only resolved incidents can be closed.";
            }

            incident.State = IncidentState.Closed;
            incident.ClosedAt = now;
            incident.ClosedById = currentUser.UserId;
            db.Set<ActivityEntry>().Add(ItsmActivity.System(Table, incident.Id, "Incident closed.", currentUser, now));
            return null;
        }, cancellationToken);

    /// <inheritdoc />
    public Task<Result> ReopenAsync(Guid id, string reason, CancellationToken cancellationToken = default) =>
        MutateAsync(id, Permissions.Incidents.Write, (db, incident, now) =>
        {
            var text = ItsmData.Clean(reason);
            if (text is null)
            {
                return "Please explain why the incident is reopened.";
            }

            if (incident.State != IncidentState.Resolved)
            {
                return "Only resolved incidents can be reopened.";
            }

            incident.State = IncidentState.InProgress;
            incident.ReopenCount++;
            incident.ResolutionCode = null;
            incident.ResolutionNotes = null;
            db.Set<ActivityEntry>().Add(ItsmActivity.System(Table, incident.Id, $"Incident reopened (#{incident.ReopenCount}): {Truncate(text)}", currentUser, now));
            return null;
        }, cancellationToken);

    /// <inheritdoc />
    public Task<Result> CancelAsync(Guid id, string reason, CancellationToken cancellationToken = default) =>
        MutateAsync(id, Permissions.Incidents.Write, (db, incident, now) =>
        {
            var text = ItsmData.Clean(reason);
            if (text is null)
            {
                return "Please explain why the incident is cancelled.";
            }

            if (!IncidentStateMachine.IsOpen(incident.State))
            {
                return $"An incident in state {ItsmLabels.For(incident.State)} cannot be cancelled.";
            }

            incident.State = IncidentState.Cancelled;
            incident.ClosedAt = now;
            incident.ClosedById = currentUser.UserId;
            db.Set<ActivityEntry>().Add(ItsmActivity.System(Table, incident.Id, $"Incident cancelled: {Truncate(text)}", currentUser, now));
            return null;
        }, cancellationToken);

    /// <inheritdoc />
    public Task<Result> AssignToMeAsync(Guid id, CancellationToken cancellationToken = default) =>
        MutateAsync(id, Permissions.Incidents.Write, (db, incident, now) =>
        {
            if (currentUser.UserId is not { } me)
            {
                return "Only users can assign records to themselves.";
            }

            if (!incident.Active)
            {
                return "Closed or cancelled incidents cannot be reassigned.";
            }

            if (incident.AssignedToId == me)
            {
                return "The incident is already assigned to you.";
            }

            incident.AssignedToId = me;
            db.Set<ActivityEntry>().Add(ItsmActivity.System(Table, incident.Id, $"Assigned to {currentUser.DisplayName}.", currentUser, now));
            return null;
        }, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<int>> AssignAsync(IReadOnlyCollection<Guid> ids, Guid? groupId, Guid? userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (!await currentUser.HasPermissionAsync(Permissions.Incidents.Write, cancellationToken))
        {
            return Result<int>.Forbidden();
        }

        var set = ids.Distinct().ToList();
        if (set.Count == 0)
        {
            return Result<int>.Invalid("Select at least one incident.");
        }

        if (set.Count > MaxBulk)
        {
            return Result<int>.Invalid($"At most {MaxBulk} incidents can be assigned at once.");
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

        var incidents = await db.Set<Incident>().Where(i => set.Contains(i.Id) && i.Active).ToListAsync(cancellationToken);
        var names = await lookup.GetUsersAsync(incidents.Select(i => i.AssignedToId).Append(userId).Where(u => u.HasValue).Select(u => u!.Value), cancellationToken);
        var groups = await lookup.GetGroupsAsync(incidents.Select(i => i.AssignmentGroupId).Append(groupId).Where(g => g.HasValue).Select(g => g!.Value), cancellationToken);
        var now = clock.UtcNow;
        foreach (var incident in incidents)
        {
            var changes = new List<(string Field, string? From, string? To)>();
            if (groupId is { } newGroup && incident.AssignmentGroupId != newGroup)
            {
                ItsmActivity.Track(changes, "Assignment group", Display(groups, incident.AssignmentGroupId), Display(groups, newGroup));
                if (incident.AssignmentGroupId is not null)
                {
                    incident.ReassignmentCount++;
                }

                incident.AssignmentGroupId = newGroup;
            }

            if (userId is { } newUser && incident.AssignedToId != newUser)
            {
                ItsmActivity.Track(changes, "Assigned to", Display(names, incident.AssignedToId), Display(names, newUser));
                incident.AssignedToId = newUser;
            }

            if (ItsmActivity.FieldChanges(Table, incident.Id, changes, currentUser, now) is { } entry)
            {
                db.Set<ActivityEntry>().Add(entry);
            }
        }

        var result = await ItsmData.SaveAsync(db, "incident", cancellationToken);
        return result.Succeeded ? Result<int>.Success(incidents.Count) : Result<int>.From(result);
    }

    /// <inheritdoc />
    public async Task<Result> LinkProblemAsync(Guid id, Guid? problemId, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Incidents.Write, cancellationToken)
            || !await currentUser.HasPermissionAsync(Permissions.Problems.Read, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var incident = await db.Set<Incident>().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (incident is null)
        {
            return Result.NotFound("The incident was not found.");
        }

        if (incident.ProblemId == problemId)
        {
            return Result.Success();
        }

        var now = clock.UtcNow;
        if (incident.ProblemId is { } previousId)
        {
            var previousNumber = await db.Set<Problem>().Where(p => p.Id == previousId).Select(p => p.Number).FirstOrDefaultAsync(cancellationToken);
            db.Set<ActivityEntry>().Add(ItsmActivity.System(TableNames.Problem, previousId, $"Incident {incident.Number} unlinked.", currentUser, now));
            db.Set<ActivityEntry>().Add(ItsmActivity.System(Table, incident.Id, $"Unlinked from problem {previousNumber}.", currentUser, now));
        }

        if (problemId is { } newId)
        {
            var problem = await db.Set<Problem>().AsNoTracking().Where(p => p.Id == newId).Select(p => new { p.Number, p.Active }).FirstOrDefaultAsync(cancellationToken);
            if (problem is null)
            {
                return Result.Invalid("The problem does not exist.");
            }

            if (!problem.Active)
            {
                return Result.Invalid("Incidents can only be linked to active problems.");
            }

            db.Set<ActivityEntry>().Add(ItsmActivity.System(Table, incident.Id, $"Linked to problem {problem.Number}.", currentUser, now));
            db.Set<ActivityEntry>().Add(ItsmActivity.System(TableNames.Problem, newId, $"Incident {incident.Number} linked.", currentUser, now));
        }

        incident.ProblemId = problemId;
        return await ItsmData.SaveAsync(db, "incident", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<IncidentListItem>>> GetChildrenAsync(Guid parentId, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Incidents.Read, cancellationToken))
        {
            return Result<IReadOnlyList<IncidentListItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Set<Incident>().AsNoTracking().Where(i => i.ParentIncidentId == parentId)
            .OrderBy(i => i.Priority).ThenByDescending(i => i.OpenedAt).Take(100).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<IncidentListItem>>.Success(await ToListItemsAsync(rows, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<IncidentListItem>>> GetByProblemAsync(Guid problemId, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Incidents.Read, cancellationToken))
        {
            return Result<IReadOnlyList<IncidentListItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Set<Incident>().AsNoTracking().Where(i => i.ProblemId == problemId)
            .OrderByDescending(i => i.Active).ThenBy(i => i.Priority).ThenByDescending(i => i.OpenedAt).Take(200).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<IncidentListItem>>.Success(await ToListItemsAsync(rows, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Incidents.Delete, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var incident = await db.Set<Incident>().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (incident is null)
        {
            return Result.NotFound("The incident was not found.");
        }

        var children = await db.Set<Incident>().Where(i => i.ParentIncidentId == id).Take(1000).ToListAsync(cancellationToken);
        foreach (var child in children)
        {
            child.ParentIncidentId = null;
        }

        db.Set<Incident>().Remove(incident);
        var result = await ItsmData.SaveAsync(db, "incident", cancellationToken);
        if (result.Succeeded)
        {
            await db.Set<ActivityEntry>().Where(a => a.TableName == Table && a.RecordId == id).ExecuteDeleteAsync(cancellationToken);
        }

        return result;
    }

    /// <summary>Applies the editable input fields to an entity.</summary>
    /// <param name="incident">The entity.</param>
    /// <param name="input">The input.</param>
    internal static void Apply(Incident incident, IncidentInput input)
    {
        incident.ShortDescription = input.ShortDescription.Trim();
        incident.Description = ItsmData.Clean(input.Description);
        incident.CallerId = input.CallerId ?? incident.CallerId;
        incident.Category = ItsmData.Clean(input.Category);
        incident.Subcategory = incident.Category is null ? null : ItsmData.Clean(input.Subcategory);
        incident.Channel = input.Channel;
        incident.Impact = input.Impact;
        incident.Urgency = input.Urgency;
        incident.AssignmentGroupId = input.AssignmentGroupId;
        incident.AssignedToId = input.AssignedToId;
        incident.ConfigurationItemId = input.ConfigurationItemId;
        incident.BusinessServiceId = input.BusinessServiceId;
        incident.IsMajorIncident = input.IsMajorIncident;
        incident.ParentIncidentId = input.ParentIncidentId;
        incident.Tags = ItsmData.Clean(input.Tags);
    }

    /// <summary>Copies the editable fields of an entity into an input object.</summary>
    /// <param name="incident">The entity.</param>
    /// <returns>The input.</returns>
    internal static IncidentInput ToInput(Incident incident) => new()
    {
        ShortDescription = incident.ShortDescription,
        Description = incident.Description,
        CallerId = incident.CallerId,
        Category = incident.Category,
        Subcategory = incident.Subcategory,
        Channel = incident.Channel,
        Impact = incident.Impact,
        Urgency = incident.Urgency,
        AssignmentGroupId = incident.AssignmentGroupId,
        AssignedToId = incident.AssignedToId,
        ConfigurationItemId = incident.ConfigurationItemId,
        BusinessServiceId = incident.BusinessServiceId,
        IsMajorIncident = incident.IsMajorIncident,
        ParentIncidentId = incident.ParentIncidentId,
        Tags = incident.Tags,
    };

    /// <summary>Counts timestamps per UTC day.</summary>
    /// <param name="values">The timestamps.</param>
    /// <param name="start">The first day (UTC midnight).</param>
    /// <param name="days">The number of days.</param>
    /// <returns>The counts, oldest first.</returns>
    internal static IReadOnlyList<double> Bucket(IEnumerable<DateTimeOffset> values, DateTimeOffset start, int days)
    {
        var buckets = new double[days];
        foreach (var value in values)
        {
            var index = (int)Math.Floor((value.ToUniversalTime() - start).TotalDays);
            if (index >= 0 && index < days)
            {
                buckets[index]++;
            }
        }

        return buckets;
    }

    private static string? Name(IReadOnlyDictionary<Guid, LookupItem> items, Guid? id) =>
        id is { } key ? items.GetValueOrDefault(key)?.Display : null;

    private static string? Display(IReadOnlyDictionary<Guid, LookupItem> items, Guid? id) => Name(items, id) ?? (id is null ? null : "(unknown)");

    private static string Truncate(string text) => text.Length > 500 ? text[..497] + "…" : text;

    private async Task<IQueryable<Incident>> ApplyFilterAsync(IAppDbContext db, IQueryable<Incident> query, IncidentQuickFilter filter, CancellationToken cancellationToken)
    {
        var me = currentUser.UserId ?? Guid.Empty;
        IQueryable<Incident> Open(IQueryable<Incident> q) =>
            q.Where(i => i.State == IncidentState.New || i.State == IncidentState.InProgress || i.State == IncidentState.OnHold);

        switch (filter)
        {
            case IncidentQuickFilter.MyOpen:
                return Open(query).Where(i => i.AssignedToId == me);
            case IncidentQuickFilter.Unassigned:
                return Open(query).Where(i => i.AssignedToId == null);
            case IncidentQuickFilter.HighPriority:
                return Open(query).Where(i => i.Priority == Priority.Critical || i.Priority == Priority.High);
            case IncidentQuickFilter.MyGroups:
                var groupIds = await db.Set<GroupMember>().AsNoTracking().Where(m => m.UserId == me).Select(m => m.GroupId).Take(200).ToListAsync(cancellationToken);
                return Open(query).Where(i => i.AssignmentGroupId != null && groupIds.Contains(i.AssignmentGroupId.Value));
            case IncidentQuickFilter.Major:
                return Open(query).Where(i => i.IsMajorIncident);
            case IncidentQuickFilter.Resolved:
                return query.Where(i => i.State == IncidentState.Resolved);
            case IncidentQuickFilter.All:
                return query;
            default:
                return query.Where(i => i.Active);
        }
    }

    private static IQueryable<Incident> ApplySearch(IAppDbContext db, IQueryable<Incident> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var t = search.Trim().ToLower();
        var people = db.Set<User>().Where(u => u.DisplayName.ToLower().Contains(t)).Select(u => u.Id);
        return query.Where(i => i.Number.ToLower().Contains(t)
            || i.ShortDescription.ToLower().Contains(t)
            || (i.Category != null && i.Category.ToLower().Contains(t))
            || (i.CallerId != null && people.Contains(i.CallerId.Value))
            || (i.AssignedToId != null && people.Contains(i.AssignedToId.Value)));
    }

    private static IQueryable<Incident> ApplySort(IQueryable<Incident> query, string? sortBy, bool descending)
    {
        IOrderedQueryable<Incident> ordered = (sortBy ?? "Priority") switch
        {
            "Number" => descending ? query.OrderByDescending(i => i.Number) : query.OrderBy(i => i.Number),
            "State" => descending ? query.OrderByDescending(i => i.State) : query.OrderBy(i => i.State),
            "ShortDescription" => descending ? query.OrderByDescending(i => i.ShortDescription) : query.OrderBy(i => i.ShortDescription),
            "Category" => descending ? query.OrderByDescending(i => i.Category) : query.OrderBy(i => i.Category),
            "OpenedAt" => descending ? query.OrderByDescending(i => i.OpenedAt) : query.OrderBy(i => i.OpenedAt),
            "UpdatedAt" => descending ? query.OrderByDescending(i => i.UpdatedAt) : query.OrderBy(i => i.UpdatedAt),
            _ => descending
                ? query.OrderByDescending(i => i.Priority).ThenByDescending(i => i.UpdatedAt)
                : query.OrderBy(i => i.Priority).ThenByDescending(i => i.UpdatedAt),
        };
        return ordered.ThenByDescending(i => i.Number);
    }

    private async Task<IReadOnlyList<IncidentListItem>> ToListItemsAsync(IReadOnlyList<Incident> rows, CancellationToken cancellationToken)
    {
        var users = await lookup.GetUsersAsync(rows.SelectMany(r => new[] { r.CallerId, r.AssignedToId }).Where(u => u.HasValue).Select(u => u!.Value), cancellationToken);
        var groups = await lookup.GetGroupsAsync(rows.Where(r => r.AssignmentGroupId.HasValue).Select(r => r.AssignmentGroupId!.Value), cancellationToken);
        return rows.Select(r => new IncidentListItem(
            r.Id,
            r.Number,
            r.ShortDescription,
            r.State,
            r.Priority,
            r.IsMajorIncident,
            r.Category,
            Name(users, r.CallerId),
            r.AssignedToId,
            Name(users, r.AssignedToId),
            Name(groups, r.AssignmentGroupId),
            r.OpenedAt,
            r.UpdatedAt)).ToList();
    }

    private async Task<List<(string Field, string? From, string? To)>> DescribeChangesAsync(
        IAppDbContext db, IncidentInput before, IncidentInput after, Priority oldPriority, Priority newPriority, CancellationToken cancellationToken)
    {
        var changes = new List<(string Field, string? From, string? To)>();
        var users = await lookup.GetUsersAsync(new[] { before.CallerId, after.CallerId, before.AssignedToId, after.AssignedToId }.Where(u => u.HasValue).Select(u => u!.Value), cancellationToken);
        var groups = await lookup.GetGroupsAsync(new[] { before.AssignmentGroupId, after.AssignmentGroupId }.Where(g => g.HasValue).Select(g => g!.Value), cancellationToken);
        var cis = await lookup.GetConfigurationItemsAsync(new[] { before.ConfigurationItemId, after.ConfigurationItemId, before.BusinessServiceId, after.BusinessServiceId }.Where(c => c.HasValue).Select(c => c!.Value), cancellationToken);
        var parentIds = new[] { before.ParentIncidentId, after.ParentIncidentId }.Where(p => p.HasValue).Select(p => p!.Value).Distinct().ToList();
        var parents = parentIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Set<Incident>().AsNoTracking().Where(i => parentIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Number, cancellationToken);

        ItsmActivity.Track(changes, "Short description", before.ShortDescription, after.ShortDescription);
        if (!string.Equals(before.Description, after.Description, StringComparison.Ordinal))
        {
            changes.Add(("Description", before.Description is null ? null : "(previous text)", after.Description is null ? null : "(updated)"));
        }

        ItsmActivity.Track(changes, "Caller", Display(users, before.CallerId), Display(users, after.CallerId));
        ItsmActivity.Track(changes, "Category", before.Category, after.Category);
        ItsmActivity.Track(changes, "Subcategory", before.Subcategory, after.Subcategory);
        ItsmActivity.Track(changes, "Channel", ItsmLabels.For(before.Channel), ItsmLabels.For(after.Channel));
        ItsmActivity.Track(changes, "Impact", ItsmLabels.For(before.Impact), ItsmLabels.For(after.Impact));
        ItsmActivity.Track(changes, "Urgency", ItsmLabels.For(before.Urgency), ItsmLabels.For(after.Urgency));
        ItsmActivity.Track(changes, "Priority", ItsmLabels.For(oldPriority), ItsmLabels.For(newPriority));
        ItsmActivity.Track(changes, "Assignment group", Display(groups, before.AssignmentGroupId), Display(groups, after.AssignmentGroupId));
        ItsmActivity.Track(changes, "Assigned to", Display(users, before.AssignedToId), Display(users, after.AssignedToId));
        ItsmActivity.Track(changes, "Configuration item", Display(cis, before.ConfigurationItemId), Display(cis, after.ConfigurationItemId));
        ItsmActivity.Track(changes, "Business service", Display(cis, before.BusinessServiceId), Display(cis, after.BusinessServiceId));
        ItsmActivity.Track(changes, "Major incident", before.IsMajorIncident ? "Yes" : "No", after.IsMajorIncident ? "Yes" : "No");
        ItsmActivity.Track(changes, "Parent incident",
            before.ParentIncidentId is { } bp ? parents.GetValueOrDefault(bp) : null,
            after.ParentIncidentId is { } ap ? parents.GetValueOrDefault(ap) : null);
        ItsmActivity.Track(changes, "Tags", before.Tags, after.Tags);
        return changes;
    }

    private async Task<Result> MutateAsync(Guid id, string permission, Func<IAppDbContext, Incident, DateTimeOffset, string?> mutate, CancellationToken cancellationToken)
    {
        if (!await currentUser.HasPermissionAsync(permission, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var incident = await db.Set<Incident>().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (incident is null)
        {
            return Result.NotFound("The incident was not found.");
        }

        if (mutate(db, incident, clock.UtcNow) is { } error)
        {
            return Result.Invalid(error);
        }

        return await ItsmData.SaveAsync(db, "incident", cancellationToken);
    }
}
