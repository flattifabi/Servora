namespace Servora.Application.Portal;

/// <summary>State filter of the "my items" list.</summary>
public enum PortalTicketFilter
{
    /// <summary>Open items (including resolved incidents awaiting confirmation).</summary>
    Open = 0,

    /// <summary>Closed, completed, rejected or cancelled items.</summary>
    Closed = 1,

    /// <summary>All items.</summary>
    All = 2,
}
