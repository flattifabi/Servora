using Servora.Domain.Common;

namespace Servora.Domain.Catalog;

/// <summary>A category of the service catalog (Hardware, Software, Access, ...).</summary>
[RecordTable(CatalogTables.Category, "Catalog Category", PluralLabel = "Catalog Categories", DisplayField = nameof(Name), Icon = "folder", Route = "/catalog-admin")]
public class CatalogCategory : AuditableEntity
{
    /// <summary>Gets or sets the unique category name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the icon name (see the UI icon set).</summary>
    public string Icon { get; set; } = "folder";

    /// <summary>Gets or sets the sort order.</summary>
    public int Order { get; set; }

    /// <summary>Gets or sets a value indicating whether the category is shown in the catalog.</summary>
    public bool IsActive { get; set; } = true;
}
