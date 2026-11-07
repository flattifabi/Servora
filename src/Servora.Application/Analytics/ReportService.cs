using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Application.Tables;
using Servora.Domain.Analytics;
using Servora.Domain.Identity;

namespace Servora.Application.Analytics;

/// <summary>Default <see cref="IReportService"/>.</summary>
/// <param name="currentUser">The current actor.</param>
/// <param name="permissions">The permission service (role membership for sharing).</param>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="registry">The table registry.</param>
/// <param name="engine">The report engine.</param>
/// <param name="audit">The audit service.</param>
/// <param name="clock">The clock.</param>
public sealed class ReportService(
    ICurrentUser currentUser,
    IPermissionService permissions,
    IAppDbContextFactory dbFactory,
    ITableRegistry registry,
    ReportEngine engine,
    IAuditService audit,
    IClock clock) : IReportService
{
    /// <summary>The maximum number of pinned reports per user.</summary>
    public const int MaxPins = 12;

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ReportTableInfo>>> GetTablesAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Analytics.Read, cancellationToken))
        {
            return Result<IReadOnlyList<ReportTableInfo>>.Forbidden();
        }

        var result = new List<ReportTableInfo>();
        foreach (var table in await registry.GetTablesAsync(cancellationToken))
        {
            if (await TableAccessPolicy.CanReadAsync(currentUser, table.Name, cancellationToken) || (table.IsCustom && await engine.CanReadAsync(table, cancellationToken)))
            {
                result.Add(new ReportTableInfo(
                    table,
                    table.Fields.Where(ReportEngine.IsGroupable).OrderBy(f => f.Label).ToList(),
                    table.Fields.Where(ReportEngine.IsNumeric).OrderBy(f => f.Label).ToList()));
            }
        }

        return Result<IReadOnlyList<ReportTableInfo>>.Success(result.OrderBy(t => t.Table.PluralLabel).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<ReportSummary>>> ListAsync(ReportListScope scope, PageRequest page, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Analytics.Read, cancellationToken))
        {
            return Result<PagedResult<ReportSummary>>.Forbidden();
        }

        var userId = currentUser.UserId ?? Guid.Empty;
        var roles = await GetRolesAsync(cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = Visible(db.Set<ReportDefinition>().AsNoTracking(), userId, roles);
        query = scope switch
        {
            ReportListScope.Mine => query.Where(r => r.OwnerId == userId),
            ReportListScope.Shared => query.Where(r => r.OwnerId != userId),
            _ => query,
        };

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim().ToLower();
            query = query.Where(r => r.Name.ToLower().Contains(term) || (r.Description != null && r.Description.ToLower().Contains(term)) || r.TableName.ToLower().Contains(term));
        }

        query = (page.SortBy, page.SortDescending) switch
        {
            ("Name", true) => query.OrderByDescending(r => r.Name),
            ("TableName", false) => query.OrderBy(r => r.TableName).ThenBy(r => r.Name),
            ("TableName", true) => query.OrderByDescending(r => r.TableName).ThenBy(r => r.Name),
            ("UpdatedAt", false) => query.OrderBy(r => r.UpdatedAt),
            ("UpdatedAt", true) => query.OrderByDescending(r => r.UpdatedAt),
            _ => query.OrderBy(r => r.Name),
        };

        var paged = await query.ToPagedResultAsync(page, cancellationToken);
        var pinned = await PinnedIdsAsync(db, userId, cancellationToken);
        var tables = (await registry.GetTablesAsync(cancellationToken)).ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        var items = paged.Items.Select(r => ToSummary(r, userId, pinned, tables)).ToList();
        return Result<PagedResult<ReportSummary>>.Success(new PagedResult<ReportSummary>(items, paged.TotalCount, paged.Page, paged.PageSize));
    }

    /// <inheritdoc />
    public async Task<Result<ReportDefinition>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Analytics.Read, cancellationToken))
        {
            return Result<ReportDefinition>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var report = await Visible(db.Set<ReportDefinition>().AsNoTracking(), currentUser.UserId ?? Guid.Empty, await GetRolesAsync(cancellationToken))
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        return report is null ? Result<ReportDefinition>.NotFound("The report was not found or is not shared with you.") : Result<ReportDefinition>.Success(report);
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> SaveAsync(ReportSaveRequest request, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Analytics.ManageReports, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        if (currentUser.UserId is not { } userId)
        {
            return Result<Guid>.Invalid("Only users can own reports.");
        }

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 200)
        {
            return Result<Guid>.Invalid("The name is required (at most 200 characters).");
        }

        if (request.Description is { Length: > 2000 })
        {
            return Result<Guid>.Invalid("The description must not exceed 2000 characters.");
        }

        var table = await registry.GetTableAsync(request.Spec.TableName, cancellationToken);
        if (table is null)
        {
            return Result<Guid>.Invalid("The selected table does not exist.");
        }

        if (!await engine.CanReadAsync(table, cancellationToken))
        {
            return Result<Guid>.Forbidden($"You do not have permission to report on {table.PluralLabel}.");
        }

        if (ReportEngine.Validate(request.Spec, table) is { } error)
        {
            return Result<Guid>.Invalid(error);
        }

        var conditionJson = ConditionSerializer.Serialize(request.Spec.Condition);
        if (conditionJson is { Length: > 16_000 })
        {
            return Result<Guid>.Invalid("The filter condition is too large.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        string? role = null;
        if (request.Visibility == ReportVisibility.Role)
        {
            role = request.SharedWithRole?.Trim();
            if (string.IsNullOrEmpty(role) || !await db.Set<Role>().AnyAsync(r => r.Name == role, cancellationToken))
            {
                return Result<Guid>.Invalid("Choose an existing role to share the report with.");
            }
        }

        ReportDefinition report;
        var created = request.Id is null;
        if (created)
        {
            report = new ReportDefinition { OwnerId = userId, OwnerName = Truncate(currentUser.DisplayName, 256) };
            db.Set<ReportDefinition>().Add(report);
        }
        else
        {
            var existing = await db.Set<ReportDefinition>().FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
            if (existing is null)
            {
                return Result<Guid>.NotFound("The report was not found.");
            }

            report = existing;
            if (report.OwnerId != userId && !await currentUser.HasPermissionAsync(Permissions.All, cancellationToken))
            {
                return Result<Guid>.Forbidden("Only the owner can change this report. Save a copy instead.");
            }
        }

        report.Name = name;
        report.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        report.TableName = table.Name;
        report.ConditionJson = conditionJson;
        report.GroupBy = string.IsNullOrEmpty(request.Spec.GroupBy) ? null : table.FindField(request.Spec.GroupBy)!.Name;
        report.DateInterval = request.Spec.DateInterval;
        report.Aggregate = request.Spec.Aggregate;
        report.AggregateField = request.Spec.Aggregate == ReportAggregate.Count ? null : table.FindField(request.Spec.AggregateField!)!.Name;
        report.ChartType = request.Spec.ChartType;
        report.MaxGroups = request.Spec.MaxGroups;
        report.Visibility = request.Visibility;
        report.SharedWithRole = role;
        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(new AuditEntry("analytics", created ? "report.created" : "report.updated")
        {
            TargetType = "sys_report",
            TargetId = report.Id.ToString(),
            TargetName = report.Name,
            Details = new { report.TableName, visibility = report.Visibility.ToString(), report.SharedWithRole },
        }, cancellationToken);
        return Result<Guid>.Success(report.Id);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Analytics.ManageReports, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var report = await db.Set<ReportDefinition>().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (report is null)
        {
            return Result.NotFound("The report was not found.");
        }

        if (report.OwnerId != currentUser.UserId && !await currentUser.HasPermissionAsync(Permissions.All, cancellationToken))
        {
            return Result.Forbidden("Only the owner can delete this report.");
        }

        await db.Set<DashboardPin>().Where(p => p.ReportId == id).ExecuteDeleteAsync(cancellationToken);
        db.Set<ReportDefinition>().Remove(report);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry("analytics", "report.deleted") { TargetType = "sys_report", TargetId = id.ToString(), TargetName = report.Name }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<ReportResult>> RunAsync(ReportSpec spec, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Analytics.Read, cancellationToken))
        {
            return Result<ReportResult>.Forbidden();
        }

        return await engine.RunAsync(spec, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<ReportResult>> RunSavedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var report = await GetAsync(id, cancellationToken);
        return report.Succeeded ? await engine.RunAsync(ReportSpec.From(report.Value!), cancellationToken) : Result<ReportResult>.From(report);
    }

    /// <inheritdoc />
    public async Task<Result> PinAsync(Guid id, bool wide = false, CancellationToken cancellationToken = default)
    {
        var report = await GetAsync(id, cancellationToken);
        if (!report.Succeeded)
        {
            return report;
        }

        if (currentUser.UserId is not { } userId)
        {
            return Result.Invalid("Only users have a personal dashboard.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var pins = await db.Set<DashboardPin>().Where(p => p.UserId == userId).ToListAsync(cancellationToken);
        var existing = pins.FirstOrDefault(p => p.ReportId == id);
        if (existing is not null)
        {
            existing.Wide = wide;
        }
        else
        {
            if (pins.Count >= MaxPins)
            {
                return Result.Invalid($"At most {MaxPins} reports can be pinned. Unpin one first.");
            }

            db.Set<DashboardPin>().Add(new DashboardPin { UserId = userId, ReportId = id, Wide = wide, PinnedAt = clock.UtcNow, Position = pins.Count == 0 ? 0 : pins.Max(p => p.Position) + 1 });
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> UnpinAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Analytics.Read, cancellationToken))
        {
            return Result.Forbidden();
        }

        var userId = currentUser.UserId ?? Guid.Empty;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Set<DashboardPin>().Where(p => p.UserId == userId && p.ReportId == id).ExecuteDeleteAsync(cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<PinnedReport>>> GetPinnedAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Analytics.Read, cancellationToken))
        {
            return Result<IReadOnlyList<PinnedReport>>.Forbidden();
        }

        var userId = currentUser.UserId ?? Guid.Empty;
        var roles = await GetRolesAsync(cancellationToken);
        List<DashboardPin> pins;
        Dictionary<Guid, ReportDefinition> reports;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            pins = await db.Set<DashboardPin>().AsNoTracking().Where(p => p.UserId == userId).OrderBy(p => p.Position).Take(MaxPins).ToListAsync(cancellationToken);
            var ids = pins.Select(p => p.ReportId).ToList();
            reports = await Visible(db.Set<ReportDefinition>().AsNoTracking(), userId, roles).Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id, cancellationToken);
        }

        var tables = (await registry.GetTablesAsync(cancellationToken)).ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        var pinnedIds = pins.Select(p => p.ReportId).ToHashSet();
        var result = new List<PinnedReport>();
        foreach (var pin in pins)
        {
            if (!reports.TryGetValue(pin.ReportId, out var report))
            {
                continue;
            }

            var summary = ToSummary(report, userId, pinnedIds, tables);
            var run = await engine.RunAsync(ReportSpec.From(report), cancellationToken);
            result.Add(new PinnedReport(summary, pin.Wide, run.Succeeded ? run.Value : null, run.Succeeded ? null : run.Error));
        }

        return Result<IReadOnlyList<PinnedReport>>.Success(result);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ShareableRole>>> GetShareableRolesAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Analytics.ManageReports, cancellationToken))
        {
            return Result<IReadOnlyList<ShareableRole>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var roles = await db.Set<Role>().AsNoTracking().OrderBy(r => r.Label).Take(200)
            .Select(r => new ShareableRole(r.Name, r.Label))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<ShareableRole>>.Success(roles);
    }

    private static IQueryable<ReportDefinition> Visible(IQueryable<ReportDefinition> source, Guid userId, List<string> roles) =>
        source.Where(r => r.OwnerId == userId
            || r.Visibility == ReportVisibility.Everyone
            || (r.Visibility == ReportVisibility.Role && r.SharedWithRole != null && roles.Contains(r.SharedWithRole)));

    private static async Task<HashSet<Guid>> PinnedIdsAsync(IAppDbContext db, Guid userId, CancellationToken cancellationToken) =>
        (await db.Set<DashboardPin>().AsNoTracking().Where(p => p.UserId == userId).Select(p => p.ReportId).ToListAsync(cancellationToken)).ToHashSet();

    private static ReportSummary ToSummary(ReportDefinition report, Guid userId, HashSet<Guid> pinned, Dictionary<string, TableDefinition> tables)
    {
        var table = tables.GetValueOrDefault(report.TableName);
        return new ReportSummary(
            report.Id,
            report.Name,
            report.Description,
            report.TableName,
            table?.PluralLabel ?? report.TableName,
            table?.Icon ?? "table",
            report.ChartType,
            report.GroupBy is null ? null : table?.FindField(report.GroupBy)?.Label ?? report.GroupBy,
            report.Visibility,
            report.SharedWithRole,
            report.OwnerName,
            report.OwnerId == userId,
            pinned.Contains(report.Id),
            report.IsSystem,
            table is not null,
            report.UpdatedAt);
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private async Task<List<string>> GetRolesAsync(CancellationToken cancellationToken) =>
        currentUser.UserId is { } userId ? [.. await permissions.GetUserRolesAsync(userId, cancellationToken)] : [];
}
