using Servora.Domain.Common;
using Servora.Domain.Itsm;

namespace Servora.Domain.Catalog;

/// <summary>A service catalog order (shopping cart checkout). Contains one or more <see cref="RequestItem"/>s.</summary>
[RecordTable(TableNames.Request, "Request", PluralLabel = "Requests", Icon = "shopping-cart", Route = "/requests")]
public class Request : TaskRecord
{
    /// <summary>Gets or sets the user the request is for.</summary>
    [Reference(TableNames.User)]
    public Guid? RequestedForId { get; set; }

    /// <summary>Gets or sets the request state (derived from the requested items).</summary>
    public RequestState State { get; set; } = RequestState.Submitted;

    /// <summary>Gets or sets the total price of all items.</summary>
    public decimal TotalPrice { get; set; }
}
