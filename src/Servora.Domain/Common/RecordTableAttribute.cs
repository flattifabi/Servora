namespace Servora.Domain.Common;

/// <summary>
/// Declares that an entity is a platform "table" (in the ServiceNow sense). Tables are discoverable by the
/// table registry and can be used by lists, forms, business rules, flows, SLAs, notifications, reports,
/// the Table REST API and MCP tools.
/// </summary>
/// <param name="name">The stable technical table name, lower snake case (for example <c>incident</c>).</param>
/// <param name="label">The human readable singular label (for example <c>Incident</c>).</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RecordTableAttribute(string name, string label) : Attribute
{
    /// <summary>Gets the technical table name.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the singular display label.</summary>
    public string Label { get; } = label;

    /// <summary>Gets or sets the plural display label. Defaults to the label with an "s" suffix.</summary>
    public string? PluralLabel { get; set; }

    /// <summary>Gets or sets the property used as the human readable display value of a record.</summary>
    public string DisplayField { get; set; } = "Number";

    /// <summary>Gets or sets the icon name (see the UI icon set) used to represent the table.</summary>
    public string Icon { get; set; } = "table";

    /// <summary>Gets or sets the relative UI route of the list view, for example <c>/incidents</c>.</summary>
    public string? Route { get; set; }
}
