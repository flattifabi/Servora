using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Directory;
using Servora.Application.Security;
using Servora.Domain.Activity;
using Servora.Domain.Common;
using Servora.Domain.Identity;
using Servora.Domain.Itsm;

namespace Servora.Application.Itsm;

/// <summary>
/// Default <see cref="IMyWorkService"/> implementation. All queries are bounded; incident and problem sections are only
/// filled when the user holds <c>incident.read</c> / <c>problem.read</c>.
/// </summary>
/// <remarks>
/// The approvals contract (<c>IApprovalService</c>) does not expose a per-user pending count yet, so
/// <see cref="MyWorkDashboard.PendingApprovals"/> is reported as <see langword="null"/> until it does.
/// </remarks>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="lookup">The directory lookup service.</param>
/// <param name="clock">The clock.</param>
public sealed class MyWorkService(IAppDbContextFactory dbFactory, ICurrentUser currentUser, IDirectoryLookupService lookup, IClock clock) : IMyWorkService
{
    private const int TrendDays = 14;
    private const int MaxItems = 15;
    private const int MaxActivity = 12;

    /// <inheritdoc />
    public async Task<Result<MyWorkDashboard>> GetAsync(CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } me)
        {
            return Result<MyWorkDashboard>.Forbidden("My Work is only available to signed-in users.");
        }

        var canIncidents = await currentUser.HasPermissionAsync(Permissions.Incidents.Read, cancellationToken);
        var canProblems = await currentUser.HasPermissionAsync(Permissions.Problems.Read, cancellationToken);
        var now = clock.UtcNow;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var groupIds = await db.Set<GroupMember>().AsNoTracking().Where(m => m.UserId == me).Select(m => m.GroupId).Take(200).ToListAsync(cancellationToken);

        var items = new List<MyWorkItem>();
        var groupNameIds = new HashSet<Guid>();
        int assigned = 0, assignedHigh = 0, groupUnassigned = 0, critical = 0, major = 0, resolvedByMe = 0;
        IReadOnlyList<double> opened = [], resolved = [];
        var byPriority = new int[5];
        var start = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddDays(-(TrendDays - 1));

        var incidentRows = new List<Incident>();
        var problemRows = new List<Problem>();
        if (canIncidents)
        {
            var open = db.Set<Incident>().AsNoTracking()
                .Where(i => i.State == IncidentState.New || i.State == IncidentState.InProgress || i.State == IncidentState.OnHold);
            var mine = open.Where(i => i.AssignedToId == me);
            assigned += await mine.CountAsync(cancellationToken);
            assignedHigh += await mine.CountAsync(i => i.Priority == Priority.Critical || i.Priority == Priority.High, cancellationToken);
            groupUnassigned += await open.CountAsync(i => i.AssignedToId == null && i.AssignmentGroupId != null && groupIds.Contains(i.AssignmentGroupId.Value), cancellationToken);
            critical = await open.CountAsync(i => i.Priority == Priority.Critical, cancellationToken);
            major = await open.CountAsync(i => i.IsMajorIncident, cancellationToken);
            var weekAgo = now.AddDays(-7);
            resolvedByMe = await db.Set<Incident>().AsNoTracking().CountAsync(i => i.ResolvedById == me && i.ResolvedAt != null && i.ResolvedAt >= weekAgo, cancellationToken);
            incidentRows = await mine.OrderBy(i => i.Priority).ThenByDescending(i => i.UpdatedAt).Take(MaxItems).ToListAsync(cancellationToken);

            var priorities = await open.GroupBy(i => i.Priority).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
            foreach (var p in priorities)
            {
                var index = (int)p.Key - 1;
                if (index is >= 0 and < 5)
                {
                    byPriority[index] = p.Count;
                }
            }

            var openedAt = await db.Set<Incident>().AsNoTracking().Where(i => i.OpenedAt >= start).Select(i => i.OpenedAt).Take(50_000).ToListAsync(cancellationToken);
            var resolvedAt = await db.Set<Incident>().AsNoTracking().Where(i => i.ResolvedAt != null && i.ResolvedAt >= start).Select(i => i.ResolvedAt!.Value).Take(50_000).ToListAsync(cancellationToken);
            opened = IncidentService.Bucket(openedAt, start, TrendDays);
            resolved = IncidentService.Bucket(resolvedAt, start, TrendDays);
        }

        if (canProblems)
        {
            var open = db.Set<Problem>().AsNoTracking()
                .Where(p => p.State == ProblemState.New || p.State == ProblemState.Assess || p.State == ProblemState.RootCauseAnalysis || p.State == ProblemState.FixInProgress);
            var mine = open.Where(p => p.AssignedToId == me);
            assigned += await mine.CountAsync(cancellationToken);
            assignedHigh += await mine.CountAsync(p => p.Priority == Priority.Critical || p.Priority == Priority.High, cancellationToken);
            groupUnassigned += await open.CountAsync(p => p.AssignedToId == null && p.AssignmentGroupId != null && groupIds.Contains(p.AssignmentGroupId.Value), cancellationToken);
            problemRows = await mine.OrderBy(p => p.Priority).ThenByDescending(p => p.UpdatedAt).Take(MaxItems).ToListAsync(cancellationToken);
        }

        var groupNames = await lookup.GetGroupsAsync(
            incidentRows.Select(i => i.AssignmentGroupId).Concat(problemRows.Select(p => p.AssignmentGroupId)).Where(g => g.HasValue).Select(g => g!.Value),
            cancellationToken);
        items.AddRange(incidentRows.Select(i => new MyWorkItem(
            TableNames.Incident, i.Id, i.Number, i.ShortDescription, ItsmLabels.For(i.State), i.Priority, i.IsMajorIncident,
            i.AssignmentGroupId is { } g ? groupNames.GetValueOrDefault(g)?.Display : null, i.OpenedAt, i.UpdatedAt, $"/incidents/{i.Id}")));
        items.AddRange(problemRows.Select(p => new MyWorkItem(
            TableNames.Problem, p.Id, p.Number, p.ShortDescription, ItsmLabels.For(p.State), p.Priority, p.IsKnownError,
            p.AssignmentGroupId is { } g ? groupNames.GetValueOrDefault(g)?.Display : null, p.OpenedAt, p.UpdatedAt, $"/problems/{p.Id}")));

        var activity = await LoadActivityAsync(db, me, canIncidents, canProblems, cancellationToken);
        var labels = Enumerable.Range(0, TrendDays).Select(d => start.AddDays(d).ToString("dd MMM", CultureInfo.InvariantCulture)).ToList();

        return Result<MyWorkDashboard>.Success(new MyWorkDashboard
        {
            CanReadIncidents = canIncidents,
            CanReadProblems = canProblems,
            AssignedToMe = assigned,
            AssignedHighPriority = assignedHigh,
            GroupUnassigned = groupUnassigned,
            GroupCount = groupIds.Count,
            CriticalOpen = critical,
            MajorOpen = major,
            PendingApprovals = null,
            ResolvedByMeThisWeek = resolvedByMe,
            Items = items.OrderBy(i => i.Priority).ThenByDescending(i => i.UpdatedAt).Take(MaxItems).ToList(),
            RecentActivity = activity,
            TrendLabels = labels,
            OpenedPerDay = opened.Count == TrendDays ? opened : new double[TrendDays],
            ResolvedPerDay = resolved.Count == TrendDays ? resolved : new double[TrendDays],
            OpenByPriority = byPriority,
        });
    }

    private static async Task<IReadOnlyList<MyWorkActivityItem>> LoadActivityAsync(IAppDbContext db, Guid me, bool canIncidents, bool canProblems, CancellationToken cancellationToken)
    {
        if (!canIncidents && !canProblems)
        {
            return [];
        }

        var incidentIds = canIncidents
            ? await db.Set<Incident>().AsNoTracking()
                .Where(i => i.Active && (i.AssignedToId == me || i.CallerId == me || i.OpenedById == me))
                .OrderByDescending(i => i.UpdatedAt).Select(i => i.Id).Take(200).ToListAsync(cancellationToken)
            : [];
        var problemIds = canProblems
            ? await db.Set<Problem>().AsNoTracking()
                .Where(p => p.Active && (p.AssignedToId == me || p.OpenedById == me))
                .OrderByDescending(p => p.UpdatedAt).Select(p => p.Id).Take(100).ToListAsync(cancellationToken)
            : [];

        var entries = await db.Set<ActivityEntry>().AsNoTracking()
            .Where(a => (canIncidents && a.TableName == TableNames.Incident && (incidentIds.Contains(a.RecordId) || a.AuthorId == me))
                || (canProblems && a.TableName == TableNames.Problem && (problemIds.Contains(a.RecordId) || a.AuthorId == me)))
            .OrderByDescending(a => a.CreatedAt)
            .Take(MaxActivity)
            .ToListAsync(cancellationToken);

        var incidentNumbers = await NumbersAsync(db.Set<Incident>(), entries.Where(e => e.TableName == TableNames.Incident).Select(e => e.RecordId), cancellationToken);
        var problemNumbers = await NumbersAsync(db.Set<Problem>(), entries.Where(e => e.TableName == TableNames.Problem).Select(e => e.RecordId), cancellationToken);

        var result = new List<MyWorkActivityItem>();
        foreach (var entry in entries)
        {
            var isIncident = entry.TableName == TableNames.Incident;
            var numbers = isIncident ? incidentNumbers : problemNumbers;
            if (!numbers.TryGetValue(entry.RecordId, out var number))
            {
                continue;
            }

            var route = isIncident ? $"/incidents/{entry.RecordId}" : $"/problems/{entry.RecordId}";
            result.Add(new MyWorkActivityItem(entry.TableName, entry.RecordId, number, route, entry.Type, entry.AuthorName, Summarize(entry), entry.CreatedAt));
        }

        return result;
    }

    private static async Task<Dictionary<Guid, string>> NumbersAsync<T>(IQueryable<T> set, IEnumerable<Guid> ids, CancellationToken cancellationToken)
        where T : TaskRecord
    {
        var list = ids.Distinct().ToList();
        return list.Count == 0
            ? new Dictionary<Guid, string>()
            : await set.AsNoTracking().Where(t => list.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Number, cancellationToken);
    }

    private static string Summarize(ActivityEntry entry)
    {
        if (entry.Type == ActivityType.FieldChange && entry.ChangesJson is { Length: > 0 } json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                var fields = document.RootElement.EnumerateArray()
                    .Select(e => e.TryGetProperty("field", out var f) ? f.GetString() : null)
                    .Where(f => !string.IsNullOrEmpty(f))
                    .ToList();
                return fields.Count == 0 ? "Updated fields" : "Updated " + string.Join(", ", fields);
            }
            catch (JsonException)
            {
                return "Updated fields";
            }
        }

        var text = (entry.Text ?? string.Empty).ReplaceLineEndings(" ");
        return text.Length > 160 ? text[..157] + "…" : text;
    }
}
