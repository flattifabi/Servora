namespace Servora.Application.Catalog;

/// <summary>A section of a catalog item's order form (read model of order forms and edit model of the catalog builder).</summary>
public sealed class CatalogSectionModel
{
    /// <summary>Gets or sets the identifier (<see langword="null"/> for new sections).</summary>
    public Guid? Id { get; set; }

    /// <summary>Gets or sets the title, for example "Who is it for?".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional one-line description shown below the title.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the display order (sections are numbered in this order).</summary>
    public int Order { get; set; }
}
