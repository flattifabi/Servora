using Servora.Domain.Common;

namespace Servora.Domain.Itsm;

/// <summary>
/// Classification option for incidents and problems. Top-level entries are categories; entries with a
/// <see cref="ParentId"/> are subcategories of that category. Records store the category <see cref="Name"/> as text so
/// renaming an option never rewrites history.
/// </summary>
[RecordTable(TableName, "Incident Category", PluralLabel = "Incident Categories", DisplayField = nameof(Name), Icon = "tag")]
public class IncidentCategory : AuditableEntity
{
    /// <summary>The technical table name.</summary>
    public const string TableName = "incident_category";

    /// <summary>Gets or sets the display name, for example <c>Network</c> or <c>VPN</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the parent category for subcategories; <see langword="null"/> for top-level categories.</summary>
    [Reference(TableName)]
    public Guid? ParentId { get; set; }

    /// <summary>Gets or sets the sort order within the parent.</summary>
    public int Order { get; set; }

    /// <summary>Gets or sets a value indicating whether the option can be selected.</summary>
    public bool IsActive { get; set; } = true;
}
