using Servora.Application.Common;

namespace Servora.Application.Portal;

/// <summary>
/// Approver support for the portal. The approval inbox itself (list, approve, reject) is the approvals module's
/// <c>IApprovalInboxService</c>; this service only explains <i>what</i> is approved. Requires <c>portal.access</c> and the
/// current user must be the approver of the approval (or hold <c>approval.read</c>).
/// </summary>
public interface IPortalApprovalService
{
    /// <summary>Gets the details of the record behind an approval (requested item answers, change window, ...).</summary>
    /// <param name="approvalId">The approval identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The subject.</returns>
    Task<Result<PortalApprovalSubject>> GetSubjectAsync(Guid approvalId, CancellationToken cancellationToken = default);
}
