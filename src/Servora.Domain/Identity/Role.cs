using Servora.Domain.Common;

namespace Servora.Domain.Identity;

/// <summary>
/// A named bundle of permissions. Roles are granted to users directly, through groups or through directory
/// group mappings.
/// </summary>
[RecordTable(TableNames.Role, "Role", PluralLabel = "Roles", DisplayField = nameof(Name), Icon = "shield", Route = "/admin/roles")]
public class Role : AuditableEntity
{
    /// <summary>Gets or sets the unique technical role name, for example <c>itil</c> or <c>admin</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the display label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the role is a built-in system role. System roles cannot be deleted
    /// and their name cannot change.
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>Gets the permissions granted by this role.</summary>
    public List<RolePermission> Permissions { get; set; } = [];
}
