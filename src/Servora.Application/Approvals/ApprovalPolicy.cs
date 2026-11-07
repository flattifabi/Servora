namespace Servora.Application.Approvals;

/// <summary>How individual approver decisions are combined.</summary>
public enum ApprovalPolicy
{
    /// <summary>The first approval approves; the first rejection rejects.</summary>
    AnyApprover = 0,

    /// <summary>Every approver must approve; any rejection rejects.</summary>
    AllApprovers = 1,
}
