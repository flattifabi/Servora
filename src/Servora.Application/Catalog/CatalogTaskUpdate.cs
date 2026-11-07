using Servora.Domain.Catalog;

namespace Servora.Application.Catalog;

/// <summary>Changes to a catalog task made by a fulfiller.</summary>
/// <param name="State">The new state.</param>
/// <param name="AssignmentGroupId">The assignment group.</param>
/// <param name="AssignedToId">The assignee.</param>
/// <param name="CloseNotes">Close notes (recommended when completing or cancelling).</param>
/// <param name="WorkNote">An optional work note recorded on the task.</param>
public sealed record CatalogTaskUpdate(CatalogTaskState State, Guid? AssignmentGroupId, Guid? AssignedToId, string? CloseNotes = null, string? WorkNote = null);
