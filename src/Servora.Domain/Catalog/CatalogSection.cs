using Servora.Domain.Common;

namespace Servora.Domain.Catalog;

/// <summary>
/// A numbered section of a catalog item's order form (for example "Who is it for?" or "Device &amp; accessories").
/// Variables reference their section; sections are numbered by <see cref="Order"/> when the form is rendered.
/// </summary>
[RecordTable(CatalogTables.Section, "Catalog Section", PluralLabel = "Catalog Sections", DisplayField = nameof(Title), Icon = "layers", Route = "/catalog-admin")]
public class CatalogSection : AuditableEntity
{
    /// <summary>Gets or sets the catalog item.</summary>
    [Reference(CatalogTables.Item)]
    public Guid CatalogItemId { get; set; }

    /// <summary>Gets or sets the section title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional one-line description shown below the title.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the display order (sections are numbered in this order).</summary>
    public int Order { get; set; }
}
