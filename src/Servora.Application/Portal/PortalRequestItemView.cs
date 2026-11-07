using Servora.Application.Catalog;

namespace Servora.Application.Portal;

/// <summary>A requested item (RITM) as shown to the requester in the portal.</summary>
public sealed record PortalRequestItemView
{
    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the number.</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>Gets the item name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the icon name.</summary>
    public string Icon { get; init; } = "package";

    /// <summary>Gets the quantity.</summary>
    public int Quantity { get; init; }

    /// <summary>Gets the unit price.</summary>
    public decimal UnitPrice { get; init; }

    /// <summary>Gets the consumer-friendly status.</summary>
    public PortalStatus Status { get; init; } = new("Submitted", "info", true);

    /// <summary>Gets the approval label, for example <c>Approved</c> (<see langword="null"/> when no approval is involved).</summary>
    public string? ApprovalLabel { get; init; }

    /// <summary>Gets the answers of the order form.</summary>
    public IReadOnlyList<VariableAnswer> Answers { get; init; } = [];

    /// <summary>Gets the number of fulfilment tasks.</summary>
    public int TaskCount { get; init; }

    /// <summary>Gets the number of finished fulfilment tasks.</summary>
    public int TasksDone { get; init; }
}
