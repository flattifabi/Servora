namespace Servora.Domain.Catalog;

/// <summary>Technical table names and number prefixes of the service catalog and request module.</summary>
public static class CatalogTables
{
    /// <summary>Catalog categories.</summary>
    public const string Category = "sc_category";

    /// <summary>Catalog items.</summary>
    public const string Item = "sc_cat_item";

    /// <summary>Catalog item variables (questions of the order form).</summary>
    public const string Variable = "item_option_new";

    /// <summary>Sections that group the variables of an order form.</summary>
    public const string Section = "sc_item_section";

    /// <summary>Catalog items users saved as favourites.</summary>
    public const string Favorite = "sc_favorite";

    /// <summary>Number prefix of requests.</summary>
    public const string RequestPrefix = "REQ";

    /// <summary>Number prefix of requested items.</summary>
    public const string RequestItemPrefix = "RITM";

    /// <summary>Number prefix of catalog tasks.</summary>
    public const string CatalogTaskPrefix = "SCTASK";
}
