namespace Servora.Application.Approvals;

/// <summary>An approver after expansion of approval groups.</summary>
/// <param name="UserId">The approver's user identifier.</param>
/// <param name="GroupId">The group through which the user became an approver, or <see langword="null"/> for direct approvers.</param>
public sealed record ResolvedApprover(Guid UserId, Guid? GroupId);
