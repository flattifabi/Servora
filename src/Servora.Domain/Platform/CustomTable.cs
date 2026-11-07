using Servora.Domain.Common;

namespace Servora.Domain.Platform;

/// <summary>
/// An administrator-defined table (the equivalent of a ServiceNow <c>sys_db_object</c> entry). Its records are stored
/// as <see cref="CustomRecord"/>s and its columns are <see cref="CustomField"/>s, so no schema migration is needed.
/// </summary>
public class CustomTable : AuditableEntity
{
    /// <summary>Gets or sets the technical table name. Always starts with <c>u_</c> to avoid collisions with typed tables.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the singular label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the plural label.</summary>
    public string PluralLabel { get; set; } = string.Empty;

    /// <summary>Gets or sets the description shown in the studio.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the icon name.</summary>
    public string Icon { get; set; } = "table";

    /// <summary>Gets or sets the field used as display value of a record (a custom field name or <c>Number</c>).</summary>
    public string? DisplayField { get; set; }

    /// <summary>Gets or sets the optional number prefix. When set, records get numbers such as <c>WRK0001001</c>.</summary>
    public string? NumberPrefix { get; set; }

    /// <summary>Gets or sets a value indicating whether the table is active (inactive tables reject new records).</summary>
    public bool IsActive { get; set; } = true;
}
