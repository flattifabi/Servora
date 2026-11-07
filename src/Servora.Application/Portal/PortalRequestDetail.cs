namespace Servora.Application.Portal;

/// <summary>A service catalog request as shown to its requester in the portal.</summary>
public sealed record PortalRequestDetail
{
    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the number.</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>Gets the title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the consumer-friendly status.</summary>
    public PortalStatus Status { get; init; } = new("Submitted", "info", true);

    /// <summary>Gets the status timeline.</summary>
    public IReadOnlyList<PortalProgressStep> Steps { get; init; } = [];

    /// <summary>Gets the requested items.</summary>
    public IReadOnlyList<PortalRequestItemView> Items { get; init; } = [];

    /// <summary>Gets the display name of the person the request is for.</summary>
    public string? RequestedForName { get; init; }

    /// <summary>Gets the display name of the person who ordered.</summary>
    public string? OpenedByName { get; init; }

    /// <summary>Gets the UTC order time.</summary>
    public DateTimeOffset OpenedAt { get; init; }

    /// <summary>Gets the UTC due time.</summary>
    public DateTimeOffset? DueAt { get; init; }

    /// <summary>Gets the UTC closing time.</summary>
    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary>Gets the total price.</summary>
    public decimal TotalPrice { get; init; }

    /// <summary>Gets a value indicating whether the requester can cancel the request.</summary>
    public bool CanCancel { get; init; }

    /// <summary>Gets a value indicating whether comments and attachments can be added.</summary>
    public bool CanComment { get; init; }
}
