namespace Servora.Application.Catalog;

/// <summary>A row of the catalog administration list.</summary>
public sealed record CatalogItemSummary
{
    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the short description.</summary>
    public string? ShortDescription { get; init; }

    /// <summary>Gets the icon.</summary>
    public string Icon { get; init; } = "package";

    /// <summary>Gets the category name.</summary>
    public string? CategoryName { get; init; }

    /// <summary>Gets the price.</summary>
    public decimal Price { get; init; }

    /// <summary>Gets the delivery time in days.</summary>
    public int DeliveryDays { get; init; }

    /// <summary>Gets a value indicating whether an approval is required.</summary>
    public bool ApprovalRequired { get; init; }

    /// <summary>Gets the fulfilment group name.</summary>
    public string? FulfillmentGroupName { get; init; }

    /// <summary>Gets the number of variables.</summary>
    public int VariableCount { get; init; }

    /// <summary>Gets a value indicating whether the item is active.</summary>
    public bool IsActive { get; init; }

    /// <summary>Gets the last update time.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
