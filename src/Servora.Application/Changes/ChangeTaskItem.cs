using Servora.Domain.Changes;

namespace Servora.Application.Changes;

/// <summary>A change task as shown on the change record page.</summary>
public sealed record ChangeTaskItem
{
    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the number.</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>Gets the short description.</summary>
    public string ShortDescription { get; init; } = string.Empty;

    /// <summary>Gets the description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the state.</summary>
    public ChangeTaskState State { get; init; }

    /// <summary>Gets the assignment group.</summary>
    public Guid? AssignmentGroupId { get; init; }

    /// <summary>Gets the assignment group name.</summary>
    public string? AssignmentGroupName { get; init; }

    /// <summary>Gets the assignee.</summary>
    public Guid? AssignedToId { get; init; }

    /// <summary>Gets the assignee name.</summary>
    public string? AssignedToName { get; init; }

    /// <summary>Gets the planned start.</summary>
    public DateTimeOffset? PlannedStartAt { get; init; }

    /// <summary>Gets the planned end.</summary>
    public DateTimeOffset? PlannedEndAt { get; init; }

    /// <summary>Gets the close notes.</summary>
    public string? CloseNotes { get; init; }
}
