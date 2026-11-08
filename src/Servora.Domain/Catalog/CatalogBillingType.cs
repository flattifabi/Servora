namespace Servora.Domain.Catalog;

/// <summary>How the price of a catalog item is billed.</summary>
public enum CatalogBillingType
{
    /// <summary>The price is charged once per unit.</summary>
    OneTime = 0,

    /// <summary>The price is charged every month for the term of the item.</summary>
    Monthly = 1,
}
