using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Application.Tables;
using Servora.Domain.Analytics;
using Servora.Domain.Common;
using Servora.Domain.Observability;

namespace Servora.Application.Analytics;

/// <summary>
/// Default <see cref="IAnalyticsService"/>. Module tables (incidents, changes, requests, SLAs, flow runs) are discovered at
/// runtime through the table registry and the EF model; every query is bounded and provider neutral (only the needed
/// columns are projected and aggregated in memory).
/// </summary>
/// <param name="currentUser">The current actor.</param>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="registry">The table registry.</param>
/// <param name="engine">The report engine (used for categorical breakdowns with labels).</param>
/// <param name="clock">The clock.</param>
public sealed class AnalyticsService(
    ICurrentUser currentUser,
    IAppDbContextFactory dbFactory,
    ITableRegistry registry,
    ReportEngine engine,
    IClock clock) : IAnalyticsService
{
    /// <summary>The maximum number of rows read per dashboard query.</summary>
    public const int MaxRows = 200_000;

    private static readonly string[] s_priorityColors = ["var(--danger)", "var(--warning)", "var(--info)", "var(--success)", "var(--text-faint)"];
    private static readonly string[] s_successStates = ["succeeded", "success", "completed", "complete", "done", "finished"];
    private static readonly string[] s_failureStates = ["failed", "failure", "error", "errored", "timedout", "timeout", "aborted"];

    /// <summary>Computes a percentage change, or null when there is no baseline.</summary>
    /// <param name="current">The current value.</param>
    /// <param name="previous">The previous value.</param>
    /// <returns>The change in percent.</returns>
    public static double? PercentChange(double current, double previous) =>
        previous == 0 ? (current == 0 ? 0 : null) : Math.Round((current - previous) / previous * 100, 1);

    /// <inheritdoc />
    public async Task<Result<ExecutiveDashboard>> GetExecutiveDashboardAsync(int periodDays = 30, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Analytics.Read, cancellationToken))
        {
            return Result<ExecutiveDashboard>.Forbidden();
        }

        var stopwatch = Stopwatch.StartNew();
        periodDays = Math.Clamp(periodDays, 7, 365);
        var buckets = new TimeBuckets(clock.UtcNow, periodDays);
        var kpis = new Dictionary<string, AnalyticsKpi>();
        var charts = new Dictionary<string, AnalyticsChart>();
        var modules = new List<string>();

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            await AddIncidentsAsync(db, buckets, kpis, charts, modules, cancellationToken);
            await AddSlaAsync(db, buckets, kpis, charts, modules, cancellationToken);
            await AddAutomationAsync(db, buckets, kpis, charts, modules, cancellationToken);
            await AddUsersAndSignInsAsync(db, buckets, kpis, charts, cancellationToken);
            await AddCmdbAsync(db, kpis, charts, modules, cancellationToken);
            await AddSecurityAsync(db, buckets, kpis, charts, cancellationToken);
        }

        charts[AnalyticsChartKeys.ChangesByState] = await BreakdownAsync(TableNames.ChangeRequest, "Change Management", ["State", "Phase", "Active"], 10, modules, cancellationToken);
        charts[AnalyticsChartKeys.RequestsByItem] = await BreakdownAsync(TableNames.RequestItem, "Service Catalog", ["CatalogItemId", "ItemId", "CatalogItem", "ItemName", "ShortDescription"], 8, modules, cancellationToken);
        charts[AnalyticsChartKeys.UsersBySource] = await BreakdownAsync(TableNames.User, null, ["Source"], 6, modules, cancellationToken);

        string[] order = ["incidents.open", "incidents.mttr", "sla.compliance", "automation.success", "users.active", "platform.signins", "cmdb.total", "security.events"];
        stopwatch.Stop();
        return Result<ExecutiveDashboard>.Success(new ExecutiveDashboard(
            periodDays,
            clock.UtcNow,
            stopwatch.ElapsedMilliseconds,
            order.Where(kpis.ContainsKey).Select(k => kpis[k]).ToList(),
            charts,
            modules.Distinct().ToList()));
    }

    private static AnalyticsKpi UnavailableKpi(string key, string label, string icon, AnalyticsAvailability availability, string caption) =>
        new(key, label, "—", caption, null, false, null, "primary", icon, availability);

    private static string Format(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static DateTimeOffset? AsDate(object? value) => value switch
    {
        DateTimeOffset dto => dto,
        DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
        _ => null,
    };

    private async Task<(TableDefinition? Table, Type? Type, AnalyticsAvailability State)> ResolveAsync(IAppDbContext db, string tableName, CancellationToken cancellationToken)
    {
        var table = await registry.GetTableAsync(tableName, cancellationToken);
        if (table?.EntityType is not { } type || !EntityQuery.IsMapped(db, type))
        {
            return (table, null, AnalyticsAvailability.ModuleMissing);
        }

        return await engine.CanReadAsync(table, cancellationToken) ? (table, type, AnalyticsAvailability.Ready) : (table, type, AnalyticsAvailability.Restricted);
    }

    private async Task AddIncidentsAsync(IAppDbContext db, TimeBuckets buckets, Dictionary<string, AnalyticsKpi> kpis, Dictionary<string, AnalyticsChart> charts, List<string> modules, CancellationToken cancellationToken)
    {
        var (table, type, state) = await ResolveAsync(db, TableNames.Incident, cancellationToken);
        if (state != AnalyticsAvailability.Ready || type is null || table is null || EntityQuery.FindProperty(type, "OpenedAt") is null)
        {
            var message = state == AnalyticsAvailability.Restricted ? "You do not have access to incidents." : "Incident Management is not installed yet.";
            kpis["incidents.open"] = UnavailableKpi("incidents.open", "Open incidents", "alert-triangle", state == AnalyticsAvailability.Ready ? AnalyticsAvailability.ModuleMissing : state, message);
            kpis["incidents.mttr"] = UnavailableKpi("incidents.mttr", "Mean time to resolve", "timer", state == AnalyticsAvailability.Ready ? AnalyticsAvailability.ModuleMissing : state, message);
            foreach (var key in new[] { AnalyticsChartKeys.IncidentTrend, AnalyticsChartKeys.IncidentBacklog, AnalyticsChartKeys.Mttr, AnalyticsChartKeys.TopCategories })
            {
                charts[key] = AnalyticsChart.Unavailable(state == AnalyticsAvailability.Ready ? AnalyticsAvailability.ModuleMissing : state, message);
            }

            return;
        }

        modules.Add("Incident Management");
        var resolvedProperty = EntityQuery.FindProperty(type, "ResolvedAt") is not null ? "ResolvedAt" : "ClosedAt";

        // Opened per bucket (+ previous period for the delta).
        var opened = (await EntityQuery.ProjectAsync(db, type, "OpenedAt", null, [EntityQuery.Since(type, "OpenedAt", buckets.PreviousStart)], MaxRows, cancellationToken))
            .Select(r => AsDate(r.Key)).OfType<DateTimeOffset>().ToList();
        var openedSeries = buckets.CountPerBucket(opened);
        var openedPrevious = opened.Count(d => d < buckets.Start);

        // Resolved per bucket and resolution times.
        var resolvedRows = await EntityQuery.ProjectAsync(db, type, resolvedProperty, "OpenedAt", [EntityQuery.Since(type, resolvedProperty, buckets.PreviousStart)], MaxRows, cancellationToken);
        var resolved = resolvedRows
            .Select(r => (Resolved: AsDate(r.Key), Opened: AsDate(r.Value)))
            .Where(r => r.Resolved is not null && r.Opened is not null)
            .Select(r => (Resolved: r.Resolved!.Value, Hours: Math.Max(0, (r.Resolved!.Value - r.Opened!.Value).TotalHours)))
            .ToList();
        var resolvedSeries = buckets.CountPerBucket(resolved.Select(r => r.Resolved));
        charts[AnalyticsChartKeys.IncidentTrend] = AnalyticsChart.FromSeries(
            buckets.Labels,
            [new AnalyticsSeries("Opened", openedSeries, "var(--primary)"), new AnalyticsSeries("Resolved", resolvedSeries, "var(--success)")],
            "No incidents were opened or resolved in this period.");

        var current = resolved.Where(r => r.Resolved >= buckets.Start).ToList();
        var previous = resolved.Where(r => r.Resolved < buckets.Start).ToList();
        var mttr = current.Count == 0 ? 0 : current.Average(r => r.Hours);
        var mttrPrevious = previous.Count == 0 ? 0 : previous.Average(r => r.Hours);
        var mttrSeries = new double[buckets.Count];
        foreach (var group in current.GroupBy(r => buckets.IndexOf(r.Resolved)).Where(g => g.Key >= 0))
        {
            mttrSeries[group.Key] = Math.Round(group.Average(r => r.Hours), 1);
        }

        charts[AnalyticsChartKeys.Mttr] = current.Count == 0
            ? AnalyticsChart.Unavailable(AnalyticsAvailability.NoData, "No incidents were resolved in this period.")
            : new AnalyticsChart { Labels = buckets.Labels, Series = [new AnalyticsSeries("MTTR (hours)", mttrSeries, "var(--info)")], Headline = Math.Round(mttr, 1), Caption = "hours on average" };

        // Open backlog by priority.
        var priorityField = table.FindField("Priority");
        var backlog = await EntityQuery.ProjectAsync(db, type, "Priority", null, [EntityQuery.EqualTo(type, "Active", true)], MaxRows, cancellationToken);
        var byPriority = backlog
            .GroupBy(r => ReportAggregator.ToDouble(r.Key) ?? 0)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var value = ((int)g.Key).ToString(CultureInfo.InvariantCulture);
                var label = priorityField?.Choices.FirstOrDefault(c => c.Value == value)?.Label ?? "P" + value;
                var color = g.Key is >= 1 and <= 5 ? s_priorityColors[(int)g.Key - 1] : null;
                return new AnalyticsSlice($"P{value} · {label}", g.Count(), color);
            })
            .ToList();
        charts[AnalyticsChartKeys.IncidentBacklog] = AnalyticsChart.FromSlices(byPriority, "No open incidents — the backlog is empty.");
        var critical = backlog.Count(r => ReportAggregator.ToDouble(r.Key) == 1);

        kpis["incidents.open"] = new AnalyticsKpi(
            "incidents.open",
            "Open incidents",
            Format(backlog.Count, "N0"),
            critical == 0 ? "no critical (P1) open" : $"{critical} critical (P1) open",
            PercentChange(openedSeries.Sum(), openedPrevious),
            true,
            openedSeries,
            critical > 0 ? "danger" : "primary",
            "alert-triangle",
            AnalyticsAvailability.Ready);
        kpis["incidents.mttr"] = new AnalyticsKpi(
            "incidents.mttr",
            "Mean time to resolve",
            current.Count == 0 ? "—" : Format(mttr, "0.0") + " h",
            $"{current.Count:N0} resolved in period",
            current.Count == 0 || previous.Count == 0 ? null : PercentChange(mttr, mttrPrevious),
            true,
            mttrSeries,
            "info",
            "timer",
            current.Count == 0 ? AnalyticsAvailability.NoData : AnalyticsAvailability.Ready);

        // Top categories of incidents opened in the period.
        if (EntityQuery.FindProperty(type, "Category") is { } categoryProperty && categoryProperty.PropertyType == typeof(string))
        {
            var categories = await EntityQuery.ProjectAsync(db, type, "Category", null, [EntityQuery.Since(type, "OpenedAt", buckets.Start)], MaxRows, cancellationToken);
            var slices = categories
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Key as string) ? "(uncategorized)" : ((string)r.Key!).Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Take(8)
                .Select(g => new AnalyticsSlice(g.Key, g.Count()))
                .ToList();
            charts[AnalyticsChartKeys.TopCategories] = AnalyticsChart.FromSlices(slices, "No incidents were opened in this period.");
        }
        else
        {
            charts[AnalyticsChartKeys.TopCategories] = AnalyticsChart.Unavailable(AnalyticsAvailability.NoData, "Incidents have no category field.");
        }
    }

    private async Task AddSlaAsync(IAppDbContext db, TimeBuckets buckets, Dictionary<string, AnalyticsKpi> kpis, Dictionary<string, AnalyticsChart> charts, List<string> modules, CancellationToken cancellationToken)
    {
        var table = (await registry.GetTablesAsync(cancellationToken)).FirstOrDefault(t => t.EntityType is not null && t.Name.Contains("sla", StringComparison.OrdinalIgnoreCase)
            && new[] { "HasBreached", "Breached", "IsBreached" }.Any(p => EntityQuery.FindProperty(t.EntityType, p) is not null));
        var type = table?.EntityType ?? EntityQuery.FindMappedType(db, "TaskSla", "TaskSlaRecord", "SlaTimer");
        var breachedProperty = type is null ? null : new[] { "HasBreached", "Breached", "IsBreached" }.FirstOrDefault(p => EntityQuery.FindProperty(type, p)?.PropertyType == typeof(bool));
        if (type is null || breachedProperty is null || !EntityQuery.IsMapped(db, type))
        {
            kpis["sla.compliance"] = UnavailableKpi("sla.compliance", "SLA compliance", "target", AnalyticsAvailability.ModuleMissing, "SLA engine not installed yet");
            charts[AnalyticsChartKeys.SlaCompliance] = AnalyticsChart.Unavailable(AnalyticsAvailability.ModuleMissing, "The SLA engine (Platform) is not installed yet.");
            return;
        }

        if (!await TableAccessPolicy.CanReadAsync(currentUser, table?.Name ?? "task_sla", cancellationToken))
        {
            kpis["sla.compliance"] = UnavailableKpi("sla.compliance", "SLA compliance", "target", AnalyticsAvailability.Restricted, "no access to SLA data");
            charts[AnalyticsChartKeys.SlaCompliance] = AnalyticsChart.Unavailable(AnalyticsAvailability.Restricted, "You do not have access to SLA data.");
            return;
        }

        modules.Add("SLA engine");
        var dateProperty = new[] { "StartedAt", "StartTime", "CreatedAt" }.FirstOrDefault(p => EntityQuery.Since(type, p, buckets.Start) is not null);
        var rows = await EntityQuery.ProjectAsync(db, type, breachedProperty, dateProperty, [dateProperty is null ? null : EntityQuery.Since(type, dateProperty, buckets.PreviousStart)], MaxRows, cancellationToken);
        var current = rows.Where(r => dateProperty is null || AsDate(r.Value) >= buckets.Start).ToList();
        var previous = rows.Where(r => dateProperty is not null && AsDate(r.Value) < buckets.Start).ToList();
        if (current.Count == 0)
        {
            kpis["sla.compliance"] = UnavailableKpi("sla.compliance", "SLA compliance", "target", AnalyticsAvailability.NoData, "no SLA timers in period");
            charts[AnalyticsChartKeys.SlaCompliance] = AnalyticsChart.Unavailable(AnalyticsAvailability.NoData, "No SLA timers started in this period.");
            return;
        }

        var breached = current.Count(r => r.Key is true);
        var compliance = (current.Count - breached) * 100.0 / current.Count;
        var previousCompliance = previous.Count == 0 ? (double?)null : (previous.Count - previous.Count(r => r.Key is true)) * 100.0 / previous.Count;
        var trend = new double[buckets.Count];
        foreach (var group in current.Where(r => AsDate(r.Value) is not null).GroupBy(r => buckets.IndexOf(AsDate(r.Value)!.Value)).Where(g => g.Key >= 0))
        {
            trend[group.Key] = Math.Round(group.Count(r => r.Key is not true) * 100.0 / group.Count(), 1);
        }

        kpis["sla.compliance"] = new AnalyticsKpi(
            "sla.compliance",
            "SLA compliance",
            Format(compliance, "0.0") + " %",
            $"{breached:N0} of {current.Count:N0} breached",
            previousCompliance is { } p ? Math.Round(compliance - p, 1) : null,
            false,
            trend,
            compliance >= 95 ? "success" : compliance >= 85 ? "warning" : "danger",
            "target",
            AnalyticsAvailability.Ready);
        charts[AnalyticsChartKeys.SlaCompliance] = new AnalyticsChart
        {
            Headline = Math.Round(compliance, 1),
            Caption = "met within target",
            Slices = [new AnalyticsSlice("Met", current.Count - breached, "var(--success)"), new AnalyticsSlice("Breached", breached, "var(--danger)")],
        };
    }

    private async Task AddAutomationAsync(IAppDbContext db, TimeBuckets buckets, Dictionary<string, AnalyticsKpi> kpis, Dictionary<string, AnalyticsChart> charts, List<string> modules, CancellationToken cancellationToken)
    {
        var tables = await registry.GetTablesAsync(cancellationToken);
        var type = tables.FirstOrDefault(t => t.EntityType is not null && t.Name is "sys_flow_context" or "flow_run" or "sys_flow_run")?.EntityType
            ?? EntityQuery.FindMappedType(db, "FlowRun", "FlowExecution", "FlowContext");
        var statusProperty = type is null ? null : new[] { "Status", "State" }.FirstOrDefault(p => EntityQuery.FindProperty(type, p) is not null);
        if (type is null || statusProperty is null || !EntityQuery.IsMapped(db, type))
        {
            kpis["automation.success"] = UnavailableKpi("automation.success", "Automation success", "workflow", AnalyticsAvailability.ModuleMissing, "Flow engine not installed yet");
            charts[AnalyticsChartKeys.AutomationSuccess] = AnalyticsChart.Unavailable(AnalyticsAvailability.ModuleMissing, "Automation (Flow Designer) is not installed yet.");
            return;
        }

        if (!await currentUser.HasPermissionAsync(Permissions.Automation.Read, cancellationToken))
        {
            kpis["automation.success"] = UnavailableKpi("automation.success", "Automation success", "workflow", AnalyticsAvailability.Restricted, "no access to automation");
            charts[AnalyticsChartKeys.AutomationSuccess] = AnalyticsChart.Unavailable(AnalyticsAvailability.Restricted, "You do not have access to automation runs.");
            return;
        }

        modules.Add("Automation");
        var dateProperty = new[] { "StartedAt", "QueuedAt", "CreatedAt" }.FirstOrDefault(p => EntityQuery.Since(type, p, buckets.Start) is not null);
        var rows = await EntityQuery.ProjectAsync(db, type, statusProperty, dateProperty, [dateProperty is null ? null : EntityQuery.Since(type, dateProperty, buckets.Start)], MaxRows, cancellationToken);
        var statuses = rows
            .GroupBy(r => Convert.ToString(r.Key, CultureInfo.InvariantCulture) ?? "Unknown", StringComparer.OrdinalIgnoreCase)
            .Select(g => (Status: g.Key, Count: g.Count()))
            .OrderByDescending(s => s.Count)
            .ToList();
        var succeeded = statuses.Where(s => s_successStates.Contains(s.Status.ToLowerInvariant())).Sum(s => s.Count);
        var failed = statuses.Where(s => s_failureStates.Contains(s.Status.ToLowerInvariant())).Sum(s => s.Count);
        var finished = succeeded + failed;
        var rate = finished == 0 ? 0 : succeeded * 100.0 / finished;

        var trend = new double[buckets.Count];
        foreach (var group in rows.Where(r => AsDate(r.Value) is not null).GroupBy(r => buckets.IndexOf(AsDate(r.Value)!.Value)).Where(g => g.Key >= 0))
        {
            var ok = group.Count(r => s_successStates.Contains((Convert.ToString(r.Key, CultureInfo.InvariantCulture) ?? string.Empty).ToLowerInvariant()));
            var bad = group.Count(r => s_failureStates.Contains((Convert.ToString(r.Key, CultureInfo.InvariantCulture) ?? string.Empty).ToLowerInvariant()));
            trend[group.Key] = ok + bad == 0 ? 0 : Math.Round(ok * 100.0 / (ok + bad), 1);
        }

        kpis["automation.success"] = new AnalyticsKpi(
            "automation.success",
            "Automation success",
            finished == 0 ? "—" : Format(rate, "0.0") + " %",
            $"{rows.Count:N0} flow run(s) in period",
            null,
            false,
            trend,
            finished == 0 ? "accent" : rate >= 95 ? "success" : rate >= 80 ? "warning" : "danger",
            "workflow",
            finished == 0 ? AnalyticsAvailability.NoData : AnalyticsAvailability.Ready);
        charts[AnalyticsChartKeys.AutomationSuccess] = statuses.Count == 0
            ? AnalyticsChart.Unavailable(AnalyticsAvailability.NoData, "No flows ran in this period.")
            : new AnalyticsChart
            {
                Headline = finished == 0 ? null : Math.Round(rate, 1),
                Caption = "success rate",
                Slices = statuses.Select(s => new AnalyticsSlice(s.Status, s.Count, s_successStates.Contains(s.Status.ToLowerInvariant()) ? "var(--success)" : s_failureStates.Contains(s.Status.ToLowerInvariant()) ? "var(--danger)" : null)).ToList(),
            };
    }

    private async Task AddUsersAndSignInsAsync(IAppDbContext db, TimeBuckets buckets, Dictionary<string, AnalyticsKpi> kpis, Dictionary<string, AnalyticsChart> charts, CancellationToken cancellationToken)
    {
        var canReadUsers = await currentUser.HasPermissionAsync(Permissions.Identity.ReadUsers, cancellationToken);
        var canReadAudit = await currentUser.HasPermissionAsync(Permissions.Observability.ReadAudit, cancellationToken);
        if (!canReadUsers && !canReadAudit)
        {
            kpis["users.active"] = UnavailableKpi("users.active", "Active users", "users", AnalyticsAvailability.Restricted, "no access to users");
            kpis["platform.signins"] = UnavailableKpi("platform.signins", "Sign-ins", "log-in", AnalyticsAvailability.Restricted, "no access to sign-in data");
            charts[AnalyticsChartKeys.SignIns] = AnalyticsChart.Unavailable(AnalyticsAvailability.Restricted, "You do not have access to sign-in data.");
            return;
        }

        var signIns = await db.Set<AuditEvent>().AsNoTracking()
            .Where(a => a.Action == "login.success" && a.OccurredAt >= buckets.PreviousStart)
            .Select(a => new { a.OccurredAt, a.ActorName })
            .Take(MaxRows)
            .ToListAsync(cancellationToken);
        var currentSignIns = signIns.Where(s => s.OccurredAt >= buckets.Start).ToList();
        var series = buckets.CountPerBucket(currentSignIns.Select(s => s.OccurredAt));
        var uniqueUsers = currentSignIns.Select(s => s.ActorName).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        kpis["platform.signins"] = new AnalyticsKpi(
            "platform.signins",
            "Sign-ins",
            Format(currentSignIns.Count, "N0"),
            $"{uniqueUsers:N0} unique user(s)",
            PercentChange(currentSignIns.Count, signIns.Count - currentSignIns.Count),
            false,
            series,
            "accent",
            "log-in",
            AnalyticsAvailability.Ready);
        charts[AnalyticsChartKeys.SignIns] = AnalyticsChart.FromSeries(buckets.Labels, [new AnalyticsSeries("Sign-ins", series, "var(--info)")], "Nobody signed in during this period.");

        if (canReadUsers)
        {
            var users = await db.Set<Domain.Identity.User>().AsNoTracking()
                .GroupBy(u => u.IsActive)
                .Select(g => new { Active = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);
            var active = users.Where(u => u.Active).Sum(u => u.Count);
            var total = users.Sum(u => u.Count);
            kpis["users.active"] = new AnalyticsKpi(
                "users.active",
                "Active users",
                Format(active, "N0"),
                $"{total - active:N0} inactive · {uniqueUsers:N0} signed in",
                null,
                false,
                null,
                "primary",
                "users",
                AnalyticsAvailability.Ready);
        }
        else
        {
            kpis["users.active"] = UnavailableKpi("users.active", "Active users", "users", AnalyticsAvailability.Restricted, "no access to users");
        }
    }

    private async Task AddCmdbAsync(IAppDbContext db, Dictionary<string, AnalyticsKpi> kpis, Dictionary<string, AnalyticsChart> charts, List<string> modules, CancellationToken cancellationToken)
    {
        var (table, type, state) = await ResolveAsync(db, TableNames.ConfigurationItem, cancellationToken);
        if (state != AnalyticsAvailability.Ready || type is null || table is null)
        {
            var message = state == AnalyticsAvailability.Restricted ? "You do not have access to the CMDB." : "The CMDB is not available.";
            kpis["cmdb.total"] = UnavailableKpi("cmdb.total", "Configuration items", "server", state, message);
            charts[AnalyticsChartKeys.CisByClass] = AnalyticsChart.Unavailable(state, message);
            return;
        }

        modules.Add("CMDB");
        var statuses = await EntityQuery.ProjectAsync(db, type, "OperationalStatus", null, [], MaxRows, cancellationToken);
        var operational = statuses.Count(r => ReportAggregator.ToDouble(r.Key) == 1);
        var percent = statuses.Count == 0 ? 0 : operational * 100.0 / statuses.Count;
        kpis["cmdb.total"] = new AnalyticsKpi(
            "cmdb.total",
            "Configuration items",
            Format(statuses.Count, "N0"),
            statuses.Count == 0 ? "CMDB is empty" : $"{percent:0.#} % operational",
            null,
            false,
            null,
            statuses.Count > 0 && percent < 90 ? "warning" : "success",
            "server",
            statuses.Count == 0 ? AnalyticsAvailability.NoData : AnalyticsAvailability.Ready);

        var result = await engine.RunAsync(new ReportSpec { TableName = table.Name, GroupBy = "ClassName", ChartType = ReportChartType.Donut, MaxGroups = 8 }, cancellationToken);
        charts[AnalyticsChartKeys.CisByClass] = result.Succeeded
            ? AnalyticsChart.FromSlices(result.Value!.Rows.Select(r => new AnalyticsSlice(r.Label, r.Value)).ToList(), "No configuration items have been recorded yet.")
            : AnalyticsChart.Unavailable(AnalyticsAvailability.Restricted, result.Error ?? "Unavailable.");
    }

    private async Task AddSecurityAsync(IAppDbContext db, TimeBuckets buckets, Dictionary<string, AnalyticsKpi> kpis, Dictionary<string, AnalyticsChart> charts, CancellationToken cancellationToken)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Observability.ReadAudit, cancellationToken))
        {
            kpis["security.events"] = UnavailableKpi("security.events", "Security events", "shield", AnalyticsAvailability.Restricted, "requires audit access");
            charts[AnalyticsChartKeys.AuditActivity] = AnalyticsChart.Unavailable(AnalyticsAvailability.Restricted, "You do not have access to the audit log.");
            return;
        }

        var events = await db.Set<AuditEvent>().AsNoTracking()
            .Where(a => a.OccurredAt >= buckets.PreviousStart)
            .Select(a => new { a.OccurredAt, a.Outcome })
            .Take(MaxRows)
            .ToListAsync(cancellationToken);
        var current = events.Where(e => e.OccurredAt >= buckets.Start).ToList();
        var success = buckets.CountPerBucket(current.Where(e => e.Outcome == AuditOutcome.Success).Select(e => e.OccurredAt));
        var failure = buckets.CountPerBucket(current.Where(e => e.Outcome == AuditOutcome.Failure).Select(e => e.OccurredAt));
        var denied = buckets.CountPerBucket(current.Where(e => e.Outcome == AuditOutcome.Denied).Select(e => e.OccurredAt));
        var problems = failure.Zip(denied, (f, d) => f + d).ToArray();
        var problemCount = problems.Sum();
        var previousProblems = events.Count(e => e.OccurredAt < buckets.Start && e.Outcome != AuditOutcome.Success);

        kpis["security.events"] = new AnalyticsKpi(
            "security.events",
            "Security events",
            Format(problemCount, "N0"),
            "failed or denied operations",
            PercentChange(problemCount, previousProblems),
            true,
            problems,
            problemCount > 0 ? "danger" : "success",
            "shield",
            AnalyticsAvailability.Ready);
        charts[AnalyticsChartKeys.AuditActivity] = AnalyticsChart.FromSeries(
            buckets.Labels,
            [new AnalyticsSeries("Successful", success, "var(--primary)"), new AnalyticsSeries("Failed", failure, "var(--warning)"), new AnalyticsSeries("Denied", denied, "var(--danger)")],
            "No audited activity in this period.");
    }

    private async Task<AnalyticsChart> BreakdownAsync(string tableName, string? moduleLabel, string[] fieldCandidates, int maxGroups, List<string> modules, CancellationToken cancellationToken)
    {
        var table = await registry.GetTableAsync(tableName, cancellationToken);
        if (table?.EntityType is null)
        {
            return AnalyticsChart.Unavailable(AnalyticsAvailability.ModuleMissing, $"{moduleLabel ?? tableName} is not installed yet.");
        }

        var field = fieldCandidates.Select(table.FindField).FirstOrDefault(f => f is not null && ReportEngine.IsGroupable(f));
        if (field is null)
        {
            return AnalyticsChart.Unavailable(AnalyticsAvailability.NoData, $"{table.PluralLabel} have no field to group by.");
        }

        var result = await engine.RunAsync(new ReportSpec { TableName = table.Name, GroupBy = field.Name, ChartType = ReportChartType.Bar, MaxGroups = maxGroups }, cancellationToken);
        if (!result.Succeeded)
        {
            return result.ErrorKind switch
            {
                ResultErrorKind.Forbidden => AnalyticsChart.Unavailable(AnalyticsAvailability.Restricted, $"You do not have access to {table.PluralLabel.ToLowerInvariant()}."),
                ResultErrorKind.NotFound => AnalyticsChart.Unavailable(AnalyticsAvailability.ModuleMissing, $"{moduleLabel ?? table.PluralLabel} is not installed yet."),
                _ => AnalyticsChart.Unavailable(AnalyticsAvailability.ModuleMissing, result.Error ?? "Unavailable."),
            };
        }

        if (moduleLabel is not null)
        {
            modules.Add(moduleLabel);
        }

        return AnalyticsChart.FromSlices(result.Value!.Rows.Select(r => new AnalyticsSlice(r.Label, r.Value)).ToList(), $"No {table.PluralLabel.ToLowerInvariant()} recorded yet.");
    }
}
