using Servora.Domain.Common;

namespace Servora.Domain.Catalog;

/// <summary>A question (variable) of a catalog item's order form.</summary>
[RecordTable(CatalogTables.Variable, "Catalog Variable", PluralLabel = "Catalog Variables", DisplayField = nameof(Label), Icon = "list", Route = "/catalog-admin")]
public class CatalogVariable : AuditableEntity
{
    /// <summary>Gets or sets the catalog item.</summary>
    [Reference(CatalogTables.Item)]
    public Guid CatalogItemId { get; set; }

    /// <summary>Gets or sets the technical name (lower snake case, unique per item). Used as key of the answers.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the question label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the input type.</summary>
    public CatalogVariableType Type { get; set; } = CatalogVariableType.Text;

    /// <summary>Gets or sets a value indicating whether an answer is mandatory.</summary>
    public bool Mandatory { get; set; }

    /// <summary>Gets or sets the display order.</summary>
    public int Order { get; set; }

    /// <summary>Gets or sets the help text shown below the input.</summary>
    public string? HelpText { get; set; }

    /// <summary>Gets or sets the options of choice variables, one per line.</summary>
    public string? Options { get; set; }

    /// <summary>Gets or sets the default value.</summary>
    public string? DefaultValue { get; set; }

    /// <summary>Gets or sets the maximum length of text answers.</summary>
    public int? MaxLength { get; set; }

    /// <summary>Gets or sets the minimum value of number answers.</summary>
    public decimal? MinValue { get; set; }

    /// <summary>Gets or sets the maximum value of number answers.</summary>
    public decimal? MaxValue { get; set; }
}
