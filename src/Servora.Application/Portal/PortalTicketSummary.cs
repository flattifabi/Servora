namespace Servora.Application.Portal;

/// <summary>One of the current user's incidents or requests as shown in portal lists.</summary>
public sealed record PortalTicketSummary
{
    /// <summary>Gets the kind of item.</summary>
    public PortalTicketKind Kind { get; init; }

    /// <summary>Gets the record identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the record number, for example <c>INC0001001</c> or <c>REQ0001001</c>.</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>Gets the title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the consumer-friendly status.</summary>
    public PortalStatus Status { get; init; } = new("Open", "info", true);

    /// <summary>Gets the icon name.</summary>
    public string Icon { get; init; } = "file-text";

    /// <summary>Gets the name of the person the item is for when it is not the current user.</summary>
    public string? OnBehalfOfName { get; init; }

    /// <summary>Gets the UTC time the item was opened.</summary>
    public DateTimeOffset OpenedAt { get; init; }

    /// <summary>Gets the UTC time of the last update.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets the relative portal route of the item.</summary>
    public string Route => Kind == PortalTicketKind.Incident ? $"/portal/incidents/{Id}" : $"/portal/requests/{Id}";
}
