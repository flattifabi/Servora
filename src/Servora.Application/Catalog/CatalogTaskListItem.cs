using Servora.Domain.Catalog;
using Servora.Domain.Itsm;

namespace Servora.Application.Catalog;

/// <summary>A catalog task row (task lists and request record page).</summary>
public sealed record CatalogTaskListItem
{
    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the number.</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>Gets the request identifier.</summary>
    public Guid RequestId { get; init; }

    /// <summary>Gets the requested item identifier.</summary>
    public Guid RequestItemId { get; init; }

    /// <summary>Gets the requested item number.</summary>
    public string? RequestItemNumber { get; init; }

    /// <summary>Gets the short description.</summary>
    public string ShortDescription { get; init; } = string.Empty;

    /// <summary>Gets the state.</summary>
    public CatalogTaskState State { get; init; }

    /// <summary>Gets the priority.</summary>
    public Priority Priority { get; init; }

    /// <summary>Gets the assignment group.</summary>
    public Guid? AssignmentGroupId { get; init; }

    /// <summary>Gets the assignment group name.</summary>
    public string? AssignmentGroupName { get; init; }

    /// <summary>Gets the assignee.</summary>
    public Guid? AssignedToId { get; init; }

    /// <summary>Gets the assignee name.</summary>
    public string? AssignedToName { get; init; }

    /// <summary>Gets the opening time.</summary>
    public DateTimeOffset OpenedAt { get; init; }

    /// <summary>Gets the due date.</summary>
    public DateTimeOffset? DueAt { get; init; }

    /// <summary>Gets the closing time.</summary>
    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary>Gets the close notes.</summary>
    public string? CloseNotes { get; init; }
}
