using Servora.Application.Common;
using Servora.Application.Security;

namespace Servora.Application.Approvals;

/// <summary>
/// Permission-checked implementation of the generic <see cref="IApprovalService"/> contract used by other modules
/// (flows, portal, custom tables). Requesting and cancelling approvals requires <c>approval.manage</c> or the write
/// permission of the record's module; reading a summary requires <c>approval.read</c> or the module's read permission.
/// </summary>
/// <param name="engine">The approval engine.</param>
/// <param name="currentUser">The current actor.</param>
public sealed class ApprovalService(ApprovalEngine engine, ICurrentUser currentUser) : IApprovalService
{
    /// <inheritdoc />
    public async Task<Result> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await CanWriteAsync(request.TableName, cancellationToken))
        {
            return Result.Forbidden();
        }

        return await engine.RequestAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApprovalSummary> GetSummaryAsync(string tableName, Guid recordId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated
            || !(await currentUser.HasPermissionAsync(Permissions.Approvals.Read, cancellationToken)
                 || await currentUser.HasPermissionAsync(ApprovalTablePermissions.ReadPermissionFor(tableName), cancellationToken)))
        {
            return new ApprovalSummary(ApprovalState.NotRequested, 0, 0, 0);
        }

        return await engine.GetSummaryAsync(tableName, recordId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result> CancelAsync(string tableName, Guid recordId, CancellationToken cancellationToken = default)
    {
        if (!await CanWriteAsync(tableName, cancellationToken))
        {
            return Result.Forbidden();
        }

        await engine.CancelAsync(tableName, recordId, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Group approvers are expanded into one approval row per member when the approval is requested, so the count
    /// covers direct and group approvals. Users may count their own approvals; counting for other users requires
    /// <c>approval.read</c> (otherwise <see langword="null"/> is returned).
    /// </remarks>
    public async Task<int?> CountPendingForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated
            || (currentUser.UserId != userId && !await currentUser.HasPermissionAsync(Permissions.Approvals.Read, cancellationToken)))
        {
            return null;
        }

        return await engine.CountPendingAsync(userId, cancellationToken);
    }

    private async Task<bool> CanWriteAsync(string tableName, CancellationToken cancellationToken) =>
        currentUser.IsAuthenticated
        && (await currentUser.HasPermissionAsync(Permissions.Approvals.Manage, cancellationToken)
            || await currentUser.HasPermissionAsync(ApprovalTablePermissions.WritePermissionFor(tableName ?? string.Empty), cancellationToken));
}
