using Servora.Domain.Approvals;
using Servora.Domain.Common;
using Servora.Domain.Itsm;

namespace Servora.Domain.Changes;

/// <summary>
/// A request to add, modify or remove anything that could affect IT services (ITIL change enablement). Normal and
/// emergency changes are authorized by the change advisory board through the generic approval engine; standard
/// changes are pre-approved.
/// </summary>
[RecordTable(TableNames.ChangeRequest, "Change Request", PluralLabel = "Change Requests", Icon = "git-pull-request", Route = "/changes")]
public class ChangeRequest : TaskRecord
{
    /// <summary>Gets or sets the change type.</summary>
    public ChangeType Type { get; set; } = ChangeType.Normal;

    /// <summary>Gets or sets the lifecycle state.</summary>
    public ChangeState State { get; set; } = ChangeState.New;

    /// <summary>Gets or sets the category, for example <c>Network</c>, <c>Software</c> or <c>Hardware</c>.</summary>
    public string? Category { get; set; }

    /// <summary>Gets or sets the user that requested the change.</summary>
    [Reference(TableNames.User)]
    public Guid? RequestedById { get; set; }

    /// <summary>Gets or sets the business justification.</summary>
    public string? Justification { get; set; }

    /// <summary>Gets or sets the step-by-step implementation plan.</summary>
    public string? ImplementationPlan { get; set; }

    /// <summary>Gets or sets the plan to restore the previous state if the change fails.</summary>
    public string? BackoutPlan { get; set; }

    /// <summary>Gets or sets the plan to verify the change after implementation.</summary>
    public string? TestPlan { get; set; }

    /// <summary>Gets or sets the calculated risk level.</summary>
    public ChangeRisk Risk { get; set; } = ChangeRisk.Low;

    /// <summary>Gets or sets the numeric risk score of the last assessment.</summary>
    public int RiskScore { get; set; }

    /// <summary>Gets or sets the answers of the risk questionnaire as JSON (question key to option index).</summary>
    public string? RiskAssessmentJson { get; set; }

    /// <summary>Gets or sets the UTC time of the last risk assessment; <see langword="null"/> when not assessed.</summary>
    public DateTimeOffset? RiskAssessedAt { get; set; }

    /// <summary>Gets or sets the UTC planned start of the implementation window.</summary>
    public DateTimeOffset? PlannedStartAt { get; set; }

    /// <summary>Gets or sets the UTC planned end of the implementation window.</summary>
    public DateTimeOffset? PlannedEndAt { get; set; }

    /// <summary>Gets or sets the UTC time implementation actually started.</summary>
    public DateTimeOffset? ActualStartAt { get; set; }

    /// <summary>Gets or sets the UTC time implementation actually ended.</summary>
    public DateTimeOffset? ActualEndAt { get; set; }

    /// <summary>Gets or sets the approval group that authorizes the change (defaults to the change advisory board).</summary>
    [Reference(TableNames.Group)]
    public Guid? ApprovalGroupId { get; set; }

    /// <summary>Gets or sets the aggregated approval status.</summary>
    public ApprovalStatus Approval { get; set; } = ApprovalStatus.NotRequested;

    /// <summary>Gets or sets the close code recorded when the change is closed.</summary>
    public ChangeCloseCode? CloseCode { get; set; }

    /// <summary>Gets or sets the closure notes.</summary>
    public string? CloseNotes { get; set; }
}
