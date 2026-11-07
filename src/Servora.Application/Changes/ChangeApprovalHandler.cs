using Microsoft.EntityFrameworkCore;
using Servora.Application.Activity;
using Servora.Application.Approvals;
using Servora.Application.Common;
using Servora.Domain.Approvals;
using Servora.Domain.Changes;
using Servora.Domain.Common;

namespace Servora.Application.Changes;

/// <summary>
/// Connects change requests to the approval engine: an approved CAB round moves the change from <c>Authorize</c> to
/// <c>Scheduled</c>; a rejected round sends it back to <c>Assess</c>.
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="activity">The activity stream service.</param>
public sealed class ChangeApprovalHandler(IAppDbContextFactory dbFactory, IActivityService activity) : IApprovalRecordHandler
{
    /// <inheritdoc />
    public string TableName => TableNames.ChangeRequest;

    /// <inheritdoc />
    public async Task<ApprovalRecordInfo?> DescribeAsync(Guid recordId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var change = await db.Set<ChangeRequest>().AsNoTracking()
            .Where(c => c.Id == recordId)
            .Select(c => new { c.Number, c.ShortDescription })
            .FirstOrDefaultAsync(cancellationToken);
        return change is null ? null : new ApprovalRecordInfo(change.Number, change.ShortDescription, $"/changes/{recordId}");
    }

    /// <inheritdoc />
    public async Task OnDecidedAsync(Guid recordId, ApprovalStatus outcome, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var change = await db.Set<ChangeRequest>().FirstOrDefaultAsync(c => c.Id == recordId, cancellationToken);
        if (change is null || change.State != ChangeState.Authorize)
        {
            return;
        }

        change.Approval = outcome;
        string message;
        if (outcome == ApprovalStatus.Approved)
        {
            change.State = ChangeState.Scheduled;
            message = "Change authorized by the approval board and scheduled for implementation.";
        }
        else
        {
            change.State = ChangeState.Assess;
            message = "Change rejected by the approval board and returned to assessment.";
        }

        await db.SaveChangesAsync(cancellationToken);
        await activity.AddSystemAsync(TableNames.ChangeRequest, recordId, message, cancellationToken);
    }
}
