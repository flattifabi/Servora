namespace Servora.Application.Changes;

/// <summary>Editable fields of a change task.</summary>
public sealed class ChangeTaskEditModel
{
    /// <summary>Gets or sets the short description.</summary>
    public string ShortDescription { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the assignment group.</summary>
    public Guid? AssignmentGroupId { get; set; }

    /// <summary>Gets or sets the assignee.</summary>
    public Guid? AssignedToId { get; set; }

    /// <summary>Gets or sets the planned start.</summary>
    public DateTimeOffset? PlannedStartAt { get; set; }

    /// <summary>Gets or sets the planned end.</summary>
    public DateTimeOffset? PlannedEndAt { get; set; }
}
