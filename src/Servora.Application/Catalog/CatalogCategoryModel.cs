namespace Servora.Application.Catalog;

/// <summary>A catalog category (list and edit model).</summary>
public sealed class CatalogCategoryModel
{
    /// <summary>Gets or sets the identifier (<see langword="null"/> for new categories).</summary>
    public Guid? Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the icon name.</summary>
    public string Icon { get; set; } = "folder";

    /// <summary>Gets or sets the sort order.</summary>
    public int Order { get; set; }

    /// <summary>Gets or sets a value indicating whether the category is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Gets or sets the number of items in the category (read only).</summary>
    public int ItemCount { get; set; }
}
