using Servora.Domain.Common;

namespace Servora.Domain.Identity;

/// <summary>A single permission key granted by a <see cref="Role"/>.</summary>
public class RolePermission : Entity
{
    /// <summary>Gets or sets the owning role identifier.</summary>
    public Guid RoleId { get; set; }

    /// <summary>
    /// Gets or sets the permission key, for example <c>incident.write</c>. The wildcard <c>*</c> grants every permission.
    /// </summary>
    public string Permission { get; set; } = string.Empty;
}
