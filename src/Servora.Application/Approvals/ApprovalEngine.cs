using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Servora.Application.Activity;
using Servora.Application.Common;
using Servora.Application.Notifications;
using Servora.Domain.Approvals;
using Servora.Domain.Identity;
using Group = Servora.Domain.Identity.Group;

namespace Servora.Application.Approvals;

/// <summary>
/// Core approval engine: group expansion, approval rounds, decisions and policy evaluation. The engine performs
/// <b>no fulfiller permission checks</b>; it must only be called by application services that authorized the caller
/// (<see cref="ApprovalService"/>, <see cref="ApprovalInboxService"/>, change and catalog services). Decisions are always
/// bound to the approver (or an approval administrator acting on their behalf).
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="clock">The clock.</param>
/// <param name="activity">The activity stream service.</param>
/// <param name="emailSender">The e-mail sender used to notify approvers.</param>
/// <param name="handlers">The record handlers of approvable tables.</param>
/// <param name="logger">The logger.</param>
public sealed partial class ApprovalEngine(
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    IClock clock,
    IActivityService activity,
    IEmailSender emailSender,
    IEnumerable<IApprovalRecordHandler> handlers,
    ILogger<ApprovalEngine> logger)
{
    /// <summary>Maximum number of approvers of one approval round.</summary>
    public const int MaxApprovers = 200;

    private const int MaxGroups = 25;
    private const int MaxNotifications = 50;
    private const int MaxReasonLength = 2000;
    private const int MaxCommentLength = 4000;

    /// <summary>Expands direct approvers and approval groups into the distinct set of active approvers.</summary>
    /// <param name="userIds">Direct approvers.</param>
    /// <param name="groupIds">Approval groups; every active member of an active group becomes an approver.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resolved approvers (direct approvers first).</returns>
    public async Task<IReadOnlyList<ResolvedApprover>> ResolveApproversAsync(IEnumerable<Guid> userIds, IEnumerable<Guid> groupIds, CancellationToken cancellationToken = default)
    {
        var users = (userIds ?? []).Where(id => id != Guid.Empty).Distinct().Take(MaxApprovers).ToList();
        var groups = (groupIds ?? []).Where(id => id != Guid.Empty).Distinct().Take(MaxGroups).ToList();
        var result = new List<ResolvedApprover>();
        var seen = new HashSet<Guid>();
        if (users.Count == 0 && groups.Count == 0)
        {
            return result;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (users.Count > 0)
        {
            var active = await db.Set<User>().AsNoTracking()
                .Where(u => users.Contains(u.Id) && u.IsActive)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
            foreach (var id in users.Where(active.Contains))
            {
                if (seen.Add(id))
                {
                    result.Add(new ResolvedApprover(id, null));
                }
            }
        }

        if (groups.Count > 0)
        {
            var members = await (
                    from member in db.Set<GroupMember>().AsNoTracking()
                    join grp in db.Set<Group>().AsNoTracking() on member.GroupId equals grp.Id
                    join user in db.Set<User>().AsNoTracking() on member.UserId equals user.Id
                    where groups.Contains(member.GroupId) && grp.IsActive && user.IsActive
                    select new { member.UserId, member.GroupId })
                .Take(MaxApprovers * 2)
                .ToListAsync(cancellationToken);

            // Keep the order of the requested groups so the "via group" attribution is deterministic.
            foreach (var member in members.OrderBy(m => groups.IndexOf(m.GroupId)))
            {
                if (result.Count >= MaxApprovers)
                {
                    break;
                }

                if (seen.Add(member.UserId))
                {
                    result.Add(new ResolvedApprover(member.UserId, member.GroupId));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Starts a new approval round for a record. Pending approvals of previous rounds are withdrawn first. Approvers are
    /// notified by e-mail (best effort) and a system entry is written to the record's activity stream.
    /// </summary>
    /// <param name="request">The approval request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifier of the new round.</returns>
    public async Task<Result<Guid>> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.TableName) || request.TableName.Length > 100 || !TableNamePattern().IsMatch(request.TableName))
        {
            return Result<Guid>.Invalid("The table name is invalid.");
        }

        if (request.RecordId == Guid.Empty)
        {
            return Result<Guid>.Invalid("The record identifier is required.");
        }

        var approvers = await ResolveApproversAsync(request.ApproverUserIds ?? [], request.ApproverGroupIds ?? [], cancellationToken);
        if (approvers.Count == 0)
        {
            return Result<Guid>.Invalid("No active approvers could be resolved. Add members to the approval group or choose other approvers.");
        }

        var handler = FindHandler(request.TableName);
        var info = handler is null ? null : await handler.DescribeAsync(request.RecordId, cancellationToken);
        if (handler is not null && info is null)
        {
            return Result<Guid>.NotFound();
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : Truncate(request.Reason.Trim(), MaxReasonLength);
        var mode = ApprovalPolicyEvaluator.ToMode(request.Policy);
        var now = clock.UtcNow;
        var batchId = Guid.CreateVersion7();

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var open = await db.Set<Approval>()
                .Where(a => a.TableName == request.TableName && a.RecordId == request.RecordId && a.State == ApprovalRecordState.Requested)
                .Take(MaxApprovers * 5)
                .ToListAsync(cancellationToken);
            foreach (var previous in open)
            {
                previous.State = ApprovalRecordState.Cancelled;
                previous.DecidedAt = now;
            }

            foreach (var approver in approvers)
            {
                db.Set<Approval>().Add(new Approval
                {
                    TableName = request.TableName,
                    RecordId = request.RecordId,
                    RecordNumber = info is null ? null : Truncate(info.Number, 40),
                    RecordSummary = Truncate(info?.Summary ?? reason ?? request.TableName, 300),
                    RecordRoute = info?.Route is null ? null : Truncate(info.Route, 300),
                    BatchId = batchId,
                    ApproverId = approver.UserId,
                    GroupId = approver.GroupId,
                    Mode = mode,
                    State = ApprovalRecordState.Requested,
                    Reason = reason,
                    RequestedById = currentUser.UserId,
                    RequestedAt = now,
                });
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        var policyText = mode == ApprovalMode.AllApprovers ? "all approvers must approve" : "any approver can decide";
        await activity.AddSystemAsync(request.TableName, request.RecordId, $"Approval requested from {approvers.Count} approver(s) ({policyText}).", cancellationToken);
        await NotifyApproversAsync(approvers.Select(a => a.UserId).ToList(), info, reason, cancellationToken);
        return Result<Guid>.Success(batchId);
    }

    /// <summary>Records an approver's decision and, when the round is decided, informs the record's handler.</summary>
    /// <param name="approvalId">The approval row identifier.</param>
    /// <param name="approve"><see langword="true"/> to approve, <see langword="false"/> to reject.</param>
    /// <param name="comment">The comment (mandatory for rejections).</param>
    /// <param name="allowOnBehalf">Whether the caller may decide approvals assigned to other approvers (approval administrators).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The aggregated status of the round after the decision.</returns>
    public async Task<Result<ApprovalStatus>> DecideAsync(Guid approvalId, bool approve, string? comment, bool allowOnBehalf, CancellationToken cancellationToken = default)
    {
        comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (!approve && comment is null)
        {
            return Result<ApprovalStatus>.Invalid("Please explain why you reject the approval.");
        }

        if (comment is { Length: > MaxCommentLength })
        {
            return Result<ApprovalStatus>.Invalid($"The comment must not exceed {MaxCommentLength} characters.");
        }

        Approval? approval;
        ApprovalStatus status;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            approval = await db.Set<Approval>().FirstOrDefaultAsync(a => a.Id == approvalId, cancellationToken);
            if (approval is null)
            {
                return Result<ApprovalStatus>.NotFound("The approval was not found.");
            }

            var onBehalf = approval.ApproverId != currentUser.UserId;
            if (onBehalf && !allowOnBehalf)
            {
                return Result<ApprovalStatus>.Forbidden("Only the assigned approver can decide this approval.");
            }

            if (approval.State != ApprovalRecordState.Requested)
            {
                return Result<ApprovalStatus>.Conflict("This approval was already decided or withdrawn.");
            }

            var round = await db.Set<Approval>()
                .Where(a => a.BatchId == approval.BatchId)
                .Take(MaxApprovers)
                .ToListAsync(cancellationToken);

            var now = clock.UtcNow;
            approval.State = approve ? ApprovalRecordState.Approved : ApprovalRecordState.Rejected;
            approval.DecidedAt = now;
            approval.DecidedById = currentUser.UserId;
            approval.Comments = onBehalf ? Truncate($"Decided on behalf of the approver by {currentUser.DisplayName}. {comment}".Trim(), MaxCommentLength) : comment;

            status = ApprovalPolicyEvaluator.Evaluate(approval.Mode, round.Select(a => a.State).ToList());
            if (ApprovalPolicyEvaluator.IsDecided(status))
            {
                foreach (var other in round.Where(a => a.State == ApprovalRecordState.Requested))
                {
                    other.State = ApprovalRecordState.NotRequired;
                    other.DecidedAt = now;
                }
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result<ApprovalStatus>.Conflict("The approval was changed by someone else. Please refresh and try again.");
            }
        }

        var verb = approve ? "approved" : "rejected";
        var text = comment is null ? $"{currentUser.DisplayName} {verb} the approval." : $"{currentUser.DisplayName} {verb} the approval: {comment}";
        await activity.AddSystemAsync(approval.TableName, approval.RecordId, text, cancellationToken);

        if (ApprovalPolicyEvaluator.IsDecided(status))
        {
            await activity.AddSystemAsync(approval.TableName, approval.RecordId, $"Approval round {(status == ApprovalStatus.Approved ? "approved" : "rejected")}.", cancellationToken);
            var handler = FindHandler(approval.TableName);
            if (handler is not null)
            {
                await handler.OnDecidedAsync(approval.RecordId, status, cancellationToken);
            }
        }

        return Result<ApprovalStatus>.Success(status);
    }

    /// <summary>Withdraws all pending approvals of a record.</summary>
    /// <param name="tableName">The technical table name.</param>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of withdrawn approvals.</returns>
    public async Task<int> CancelAsync(string tableName, Guid recordId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var open = await db.Set<Approval>()
            .Where(a => a.TableName == tableName && a.RecordId == recordId && a.State == ApprovalRecordState.Requested)
            .Take(MaxApprovers * 5)
            .ToListAsync(cancellationToken);
        if (open.Count == 0)
        {
            return 0;
        }

        var now = clock.UtcNow;
        foreach (var approval in open)
        {
            approval.State = ApprovalRecordState.Cancelled;
            approval.DecidedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        await activity.AddSystemAsync(tableName, recordId, $"{open.Count} pending approval(s) withdrawn.", cancellationToken);
        return open.Count;
    }

    /// <summary>Gets the aggregated approval state of the latest approval round of a record.</summary>
    /// <param name="tableName">The technical table name.</param>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The summary.</returns>
    public async Task<ApprovalSummary> GetSummaryAsync(string tableName, Guid recordId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Set<Approval>().AsNoTracking()
            .Where(a => a.TableName == tableName && a.RecordId == recordId)
            .Select(a => new { a.BatchId, a.RequestedAt, a.Mode, a.State })
            .Take(MaxApprovers * 10)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return new ApprovalSummary(ApprovalState.NotRequested, 0, 0, 0);
        }

        var latest = rows.GroupBy(r => r.BatchId).OrderByDescending(g => g.Max(r => r.RequestedAt)).First().ToList();
        var status = ApprovalPolicyEvaluator.Evaluate(latest[0].Mode, latest.Select(r => r.State).ToList());
        return new ApprovalSummary(
            ApprovalPolicyEvaluator.ToContract(status),
            latest.Count(r => r.State == ApprovalRecordState.Requested),
            latest.Count(r => r.State == ApprovalRecordState.Approved),
            latest.Count(r => r.State == ApprovalRecordState.Rejected));
    }

    /// <summary>Counts the approvals waiting for a decision by a user.</summary>
    /// <param name="userId">The approver.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of pending approvals.</returns>
    public async Task<int> CountPendingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<Approval>().CountAsync(a => a.ApproverId == userId && a.State == ApprovalRecordState.Requested, cancellationToken);
    }

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex TableNamePattern();

    private IApprovalRecordHandler? FindHandler(string tableName) =>
        handlers.FirstOrDefault(h => string.Equals(h.TableName, tableName, StringComparison.OrdinalIgnoreCase));

    private async Task NotifyApproversAsync(IReadOnlyList<Guid> approverIds, ApprovalRecordInfo? info, string? reason, CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var ids = approverIds.Take(MaxNotifications).ToList();
            var recipients = await db.Set<User>().AsNoTracking()
                .Where(u => ids.Contains(u.Id) && u.Email != null && u.Email != string.Empty)
                .Select(u => new { u.DisplayName, u.Email })
                .ToListAsync(cancellationToken);

            var title = info is null ? "Approval requested" : $"Approval requested: {info.Number}";
            foreach (var recipient in recipients)
            {
                var html = $"""
                    <p>Hello {WebUtility.HtmlEncode(recipient.DisplayName)},</p>
                    <p>Your approval is requested for <b>{WebUtility.HtmlEncode(info?.Number ?? "a record")}</b>: {WebUtility.HtmlEncode(info?.Summary ?? string.Empty)}</p>
                    {(reason is null ? string.Empty : $"<p>{WebUtility.HtmlEncode(reason)}</p>")}
                    <p>Open <b>My approvals</b> in Servora to approve or reject.</p>
                    """;
                await emailSender.SendAsync(new EmailMessage([recipient.Email!], title, html), cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Notifications are best effort: an unavailable mail server must never block approvals.
            logger.LogWarning(ex, "Approval notification failed.");
        }
    }
}
