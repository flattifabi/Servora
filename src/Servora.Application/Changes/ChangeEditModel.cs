using Servora.Domain.Changes;
using Servora.Domain.Itsm;

namespace Servora.Application.Changes;

/// <summary>Editable fields of a change request (create and update form).</summary>
public sealed class ChangeEditModel
{
    /// <summary>Gets or sets the short description.</summary>
    public string ShortDescription { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the type (only changeable while the change is new).</summary>
    public ChangeType Type { get; set; } = ChangeType.Normal;

    /// <summary>Gets or sets the category.</summary>
    public string? Category { get; set; }

    /// <summary>Gets or sets the impact.</summary>
    public Impact Impact { get; set; } = Impact.Medium;

    /// <summary>Gets or sets the urgency.</summary>
    public Urgency Urgency { get; set; } = Urgency.Medium;

    /// <summary>Gets or sets the assignment group.</summary>
    public Guid? AssignmentGroupId { get; set; }

    /// <summary>Gets or sets the assignee.</summary>
    public Guid? AssignedToId { get; set; }

    /// <summary>Gets or sets the requester.</summary>
    public Guid? RequestedById { get; set; }

    /// <summary>Gets or sets the affected configuration item.</summary>
    public Guid? ConfigurationItemId { get; set; }

    /// <summary>Gets or sets the approval group (defaults to the change advisory board).</summary>
    public Guid? ApprovalGroupId { get; set; }

    /// <summary>Gets or sets the planned start (UTC).</summary>
    public DateTimeOffset? PlannedStartAt { get; set; }

    /// <summary>Gets or sets the planned end (UTC).</summary>
    public DateTimeOffset? PlannedEndAt { get; set; }

    /// <summary>Gets or sets the justification.</summary>
    public string? Justification { get; set; }

    /// <summary>Gets or sets the implementation plan.</summary>
    public string? ImplementationPlan { get; set; }

    /// <summary>Gets or sets the backout plan.</summary>
    public string? BackoutPlan { get; set; }

    /// <summary>Gets or sets the test plan.</summary>
    public string? TestPlan { get; set; }

    /// <summary>Creates an edit model from a change detail.</summary>
    /// <param name="detail">The detail.</param>
    /// <returns>The model.</returns>
    public static ChangeEditModel From(ChangeDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        return new ChangeEditModel
        {
            ShortDescription = detail.ShortDescription,
            Description = detail.Description,
            Type = detail.Type,
            Category = detail.Category,
            Impact = detail.Impact,
            Urgency = detail.Urgency,
            AssignmentGroupId = detail.AssignmentGroupId,
            AssignedToId = detail.AssignedToId,
            RequestedById = detail.RequestedById,
            ConfigurationItemId = detail.ConfigurationItemId,
            ApprovalGroupId = detail.ApprovalGroupId,
            PlannedStartAt = detail.PlannedStartAt,
            PlannedEndAt = detail.PlannedEndAt,
            Justification = detail.Justification,
            ImplementationPlan = detail.ImplementationPlan,
            BackoutPlan = detail.BackoutPlan,
            TestPlan = detail.TestPlan,
        };
    }
}
