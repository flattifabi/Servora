using Microsoft.EntityFrameworkCore;
using Servora.Application.Approvals;
using Servora.Application.Common;
using Servora.Domain.Approvals;
using Servora.Domain.Catalog;
using Servora.Domain.Common;
using Servora.Domain.Identity;

namespace Servora.Application.Catalog;

/// <summary>
/// Connects requested items to the approval engine: an approved round starts fulfilment (catalog tasks), a rejected
/// round closes the item as rejected.
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="fulfillment">The fulfilment engine.</param>
public sealed class RequestItemApprovalHandler(IAppDbContextFactory dbFactory, CatalogFulfillmentEngine fulfillment) : IApprovalRecordHandler
{
    /// <inheritdoc />
    public string TableName => TableNames.RequestItem;

    /// <inheritdoc />
    public async Task<ApprovalRecordInfo?> DescribeAsync(Guid recordId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Set<RequestItem>().AsNoTracking()
            .Where(r => r.Id == recordId)
            .Select(r => new { r.Number, r.ShortDescription, r.RequestId, r.RequestedForId, r.Quantity })
            .FirstOrDefaultAsync(cancellationToken);
        if (item is null)
        {
            return null;
        }

        var requestedFor = item.RequestedForId is { } userId
            ? await db.Set<User>().AsNoTracking().Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefaultAsync(cancellationToken)
            : null;
        var summary = item.ShortDescription + (item.Quantity > 1 ? $" (×{item.Quantity})" : string.Empty) + (requestedFor is null ? string.Empty : $" for {requestedFor}");
        return new ApprovalRecordInfo(item.Number, summary, $"/requests/{item.RequestId}");
    }

    /// <inheritdoc />
    public async Task OnDecidedAsync(Guid recordId, ApprovalStatus outcome, CancellationToken cancellationToken)
    {
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var item = await db.Set<RequestItem>().FirstOrDefaultAsync(r => r.Id == recordId, cancellationToken);
            if (item is null || item.State != RequestItemState.PendingApproval)
            {
                return;
            }

            item.Approval = outcome;
            await db.SaveChangesAsync(cancellationToken);
        }

        if (outcome == ApprovalStatus.Approved)
        {
            await fulfillment.StartFulfillmentAsync(recordId, cancellationToken);
        }
        else
        {
            await fulfillment.RejectAsync(recordId, cancellationToken);
        }
    }
}
