namespace Servora.Domain.Approvals;

/// <summary>How the individual decisions of one approval round are combined (stored with every approval row).</summary>
public enum ApprovalMode
{
    /// <summary>The first approval approves the record; the first rejection rejects it.</summary>
    AnyApprover = 0,

    /// <summary>Every approver must approve; any rejection rejects the record.</summary>
    AllApprovers = 1,
}
