using Servora.Domain.Common;
using Servora.Domain.Itsm;

namespace Servora.Domain.Catalog;

/// <summary>A fulfilment task of a <see cref="RequestItem"/>, worked by the item's fulfilment group.</summary>
[RecordTable(TableNames.CatalogTask, "Catalog Task", PluralLabel = "Catalog Tasks", Icon = "clipboard-list", Route = "/requests")]
public class CatalogTask : TaskRecord
{
    /// <summary>Gets or sets the requested item.</summary>
    [Reference(TableNames.RequestItem)]
    public Guid RequestItemId { get; set; }

    /// <summary>Gets or sets the request (denormalized for fast navigation).</summary>
    [Reference(TableNames.Request)]
    public Guid RequestId { get; set; }

    /// <summary>Gets or sets the task state.</summary>
    public CatalogTaskState State { get; set; } = CatalogTaskState.Open;

    /// <summary>Gets or sets the closure notes.</summary>
    public string? CloseNotes { get; set; }

    /// <summary>Gets or sets the order within the requested item.</summary>
    public int Order { get; set; }
}
