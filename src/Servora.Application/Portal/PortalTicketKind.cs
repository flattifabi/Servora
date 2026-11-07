namespace Servora.Application.Portal;

/// <summary>The kinds of records a portal user owns ("my items").</summary>
public enum PortalTicketKind
{
    /// <summary>An incident ("issue") the user reported or is the caller of.</summary>
    Incident = 0,

    /// <summary>A service catalog request the user ordered or receives.</summary>
    Request = 1,
}
