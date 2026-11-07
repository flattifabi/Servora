using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Application.Tables;
using Servora.Domain.Approvals;
using Servora.Domain.Identity;

namespace Servora.Application.Approvals;

/// <summary>Default <see cref="IApprovalInboxService"/> implementation.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="engine">The approval engine.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="tables">The table registry (table labels).</param>
/// <param name="clock">The clock.</param>
public sealed class ApprovalInboxService(
    IAppDbContextFactory dbFactory,
    ApprovalEngine engine,
    ICurrentUser currentUser,
    ITableRegistry tables,
    IClock clock) : IApprovalInboxService
{
    private const int MaxRecordApprovals = 500;

    /// <inheritdoc />
    public async Task<Result<PagedResult<ApprovalItem>>> ListAsync(ApprovalInboxFilter filter, PageRequest page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        var userId = currentUser.UserId;
        var all = filter is ApprovalInboxFilter.AllPending or ApprovalInboxFilter.All;
        if (all && !await currentUser.HasPermissionAsync(Permissions.Approvals.Read, cancellationToken))
        {
            return Result<PagedResult<ApprovalItem>>.Forbidden();
        }

        if (!all && userId is null)
        {
            return Result<PagedResult<ApprovalItem>>.Forbidden("Only users have personal approvals.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<Approval>().AsNoTracking();
        query = filter switch
        {
            ApprovalInboxFilter.MyPending => query.Where(a => a.ApproverId == userId && a.State == ApprovalRecordState.Requested),
            ApprovalInboxFilter.MyHistory => query.Where(a => a.ApproverId == userId && a.State != ApprovalRecordState.Requested),
            ApprovalInboxFilter.AllPending => query.Where(a => a.State == ApprovalRecordState.Requested),
            _ => query,
        };

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim().ToLower();
            query = query.Where(a => (a.RecordNumber != null && a.RecordNumber.ToLower().Contains(term))
                                     || (a.RecordSummary != null && a.RecordSummary.ToLower().Contains(term)));
        }

        query = filter is ApprovalInboxFilter.MyPending or ApprovalInboxFilter.AllPending
            ? query.OrderBy(a => a.RequestedAt)
            : query.OrderByDescending(a => a.RequestedAt);

        var paged = await query.ToPagedResultAsync(page, cancellationToken);
        var manage = await currentUser.HasPermissionAsync(Permissions.Approvals.Manage, cancellationToken);
        var items = await MapAsync(db, paged.Items, manage, cancellationToken);
        return Result<PagedResult<ApprovalItem>>.Success(new PagedResult<ApprovalItem>(items, paged.TotalCount, paged.Page, paged.PageSize));
    }

    /// <inheritdoc />
    public async Task<Result<ApprovalInboxStats>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<ApprovalInboxStats>.Success(new ApprovalInboxStats(0, 0, 0, null));
        }

        var since = clock.UtcNow.AddDays(-30);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var mine = db.Set<Approval>().AsNoTracking().Where(a => a.ApproverId == userId);
        var pending = await mine.CountAsync(a => a.State == ApprovalRecordState.Requested, cancellationToken);
        var approved = await mine.CountAsync(a => a.State == ApprovalRecordState.Approved && a.DecidedAt >= since, cancellationToken);
        var rejected = await mine.CountAsync(a => a.State == ApprovalRecordState.Rejected && a.DecidedAt >= since, cancellationToken);
        var oldest = pending == 0
            ? (DateTimeOffset?)null
            : await mine.Where(a => a.State == ApprovalRecordState.Requested).OrderBy(a => a.RequestedAt).Select(a => (DateTimeOffset?)a.RequestedAt).FirstOrDefaultAsync(cancellationToken);
        return Result<ApprovalInboxStats>.Success(new ApprovalInboxStats(pending, approved, rejected, oldest));
    }

    /// <inheritdoc />
    public Task<Result> ApproveAsync(Guid approvalId, string? comment, CancellationToken cancellationToken = default) =>
        DecideAsync(approvalId, true, comment, cancellationToken);

    /// <inheritdoc />
    public Task<Result> RejectAsync(Guid approvalId, string comment, CancellationToken cancellationToken = default) =>
        DecideAsync(approvalId, false, comment, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ApprovalItem>>> GetForRecordAsync(string tableName, Guid recordId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(tableName))
        {
            return Result<IReadOnlyList<ApprovalItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Set<Approval>().AsNoTracking()
            .Where(a => a.TableName == tableName && a.RecordId == recordId)
            .OrderByDescending(a => a.RequestedAt)
            .Take(MaxRecordApprovals)
            .ToListAsync(cancellationToken);

        var allowed = await currentUser.HasPermissionAsync(Permissions.Approvals.Read, cancellationToken)
                      || await currentUser.HasPermissionAsync(ApprovalTablePermissions.ReadPermissionFor(tableName), cancellationToken)
                      || (currentUser.UserId is { } me && rows.Any(r => r.ApproverId == me));
        if (!allowed)
        {
            return Result<IReadOnlyList<ApprovalItem>>.Forbidden();
        }

        var manage = await currentUser.HasPermissionAsync(Permissions.Approvals.Manage, cancellationToken);
        var items = await MapAsync(db, rows, manage, cancellationToken);
        return Result<IReadOnlyList<ApprovalItem>>.Success(items);
    }

    private async Task<Result> DecideAsync(Guid approvalId, bool approve, string? comment, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null)
        {
            return Result.Forbidden("Only users can decide approvals.");
        }

        var manage = await currentUser.HasPermissionAsync(Permissions.Approvals.Manage, cancellationToken);
        var result = await engine.DecideAsync(approvalId, approve, comment, manage, cancellationToken);
        return result.Succeeded ? Result.Success() : result;
    }

    private async Task<IReadOnlyList<ApprovalItem>> MapAsync(IAppDbContext db, IReadOnlyList<Approval> rows, bool manage, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var userIds = rows.Select(r => r.ApproverId)
            .Concat(rows.Where(r => r.RequestedById.HasValue).Select(r => r.RequestedById!.Value))
            .Distinct()
            .ToList();
        var groupIds = rows.Where(r => r.GroupId.HasValue).Select(r => r.GroupId!.Value).Distinct().ToList();
        var users = await db.Set<User>().AsNoTracking().Where(u => userIds.Contains(u.Id)).Select(u => new { u.Id, u.DisplayName }).ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);
        var groups = groupIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Set<Group>().AsNoTracking().Where(g => groupIds.Contains(g.Id)).Select(g => new { g.Id, g.Name }).ToDictionaryAsync(g => g.Id, g => g.Name, cancellationToken);

        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in rows.Select(r => r.TableName).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            labels[table] = (await tables.GetTableAsync(table, cancellationToken))?.Label ?? table;
        }

        var me = currentUser.UserId;
        return rows.Select(r => new ApprovalItem
        {
            Id = r.Id,
            TableName = r.TableName,
            TableLabel = labels.GetValueOrDefault(r.TableName, r.TableName),
            RecordId = r.RecordId,
            RecordNumber = r.RecordNumber,
            RecordSummary = r.RecordSummary,
            RecordRoute = r.RecordRoute,
            Reason = r.Reason,
            State = r.State,
            Mode = r.Mode,
            ApproverId = r.ApproverId,
            ApproverName = users.GetValueOrDefault(r.ApproverId, "Unknown user"),
            GroupName = r.GroupId is { } g ? groups.GetValueOrDefault(g) : null,
            RequestedByName = r.RequestedById is { } u ? users.GetValueOrDefault(u) : null,
            RequestedAt = r.RequestedAt,
            DecidedAt = r.DecidedAt,
            Comments = r.Comments,
            IsMine = me == r.ApproverId,
            CanDecide = r.State == ApprovalRecordState.Requested && (me == r.ApproverId || (manage && me is not null)),
        }).ToList();
    }
}
