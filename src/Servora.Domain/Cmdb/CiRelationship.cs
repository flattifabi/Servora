using Servora.Domain.Common;

namespace Servora.Domain.Cmdb;

/// <summary>A directed relationship between two configuration items (<c>cmdb_rel_ci</c>): "parent &lt;type&gt; child".</summary>
[RecordTable(CmdbTables.Relationship, "CI Relationship", PluralLabel = "CI Relationships", DisplayField = nameof(Type), Icon = "network")]
public class CiRelationship : AuditableEntity
{
    /// <summary>Gets or sets the parent (upstream, consuming) CI.</summary>
    [Reference(TableNames.ConfigurationItem)]
    public Guid ParentId { get; set; }

    /// <summary>Gets or sets the child (downstream, providing) CI.</summary>
    [Reference(TableNames.ConfigurationItem)]
    public Guid ChildId { get; set; }

    /// <summary>Gets or sets the relationship type.</summary>
    public CiRelationshipType Type { get; set; } = CiRelationshipType.DependsOn;

    /// <summary>Gets or sets an optional note (port, protocol, ...).</summary>
    public string? Note { get; set; }
}
