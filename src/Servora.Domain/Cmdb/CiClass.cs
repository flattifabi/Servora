using Servora.Domain.Common;

namespace Servora.Domain.Cmdb;

/// <summary>
/// A CI class (<c>cmdb_class</c>) such as Server, Application or Business Service. Classes form a hierarchy through
/// <see cref="ParentKey"/> and define class-specific attributes as a JSON schema (<see cref="AttributesJson"/>, an array of
/// <see cref="CiAttributeDefinition"/>) that are rendered as custom fields on the CI form.
/// </summary>
[RecordTable(CmdbTables.CiClass, "CI Class", PluralLabel = "CI Classes", DisplayField = nameof(Label), Icon = "layers", Route = "/cmdb/classes")]
public class CiClass : AuditableEntity
{
    /// <summary>Gets or sets the unique technical key, lower snake case (for example <c>business_service</c>).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the display label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the icon name of the design system icon set.</summary>
    public string Icon { get; set; } = "box";

    /// <summary>Gets or sets the accent color (CSS hex) used in the dependency graph.</summary>
    public string Color { get; set; } = "#7c6cff";

    /// <summary>Gets or sets the key of the parent class, or <see langword="null"/> for root classes.</summary>
    public string? ParentKey { get; set; }

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the JSON array of class-specific attribute definitions.</summary>
    public string? AttributesJson { get; set; }

    /// <summary>Gets or sets a value indicating whether the class ships with the product (cannot be deleted).</summary>
    public bool IsSystem { get; set; }

    /// <summary>Gets or sets the display order.</summary>
    public int SortOrder { get; set; }
}
