using Servora.Domain.Catalog;

namespace Servora.Web.Components.ServiceManagement;

/// <summary>Mutable form state of the catalog task drawer on the request record page.</summary>
public sealed class CatalogTaskForm
{
    /// <summary>Gets or sets the target state.</summary>
    public CatalogTaskState State { get; set; }

    /// <summary>Gets or sets the assignment group.</summary>
    public Guid? GroupId { get; set; }

    /// <summary>Gets or sets the assignee.</summary>
    public Guid? AssignedToId { get; set; }

    /// <summary>Gets or sets the close notes.</summary>
    public string? CloseNotes { get; set; }

    /// <summary>Gets or sets the work note.</summary>
    public string? WorkNote { get; set; }
}
