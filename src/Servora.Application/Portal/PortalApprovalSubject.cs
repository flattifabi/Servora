using Servora.Application.Catalog;

namespace Servora.Application.Portal;

/// <summary>What an approver is asked to approve, shown in the portal approvals view.</summary>
public sealed record PortalApprovalSubject
{
    /// <summary>Gets the approval identifier.</summary>
    public Guid ApprovalId { get; init; }

    /// <summary>Gets the title of the approved record.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the icon name.</summary>
    public string Icon { get; init; } = "check-square";

    /// <summary>Gets a plain-text description of the record.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the key facts (requested for, quantity, price, planned window, risk, ...).</summary>
    public IReadOnlyList<PortalFact> Facts { get; init; } = [];

    /// <summary>Gets the order form answers (requested items only).</summary>
    public IReadOnlyList<VariableAnswer> Answers { get; init; } = [];
}
