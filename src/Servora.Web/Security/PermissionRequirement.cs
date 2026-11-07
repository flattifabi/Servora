using Microsoft.AspNetCore.Authorization;

namespace Servora.Web.Security;

/// <summary>Authorization requirement for a single permission key.</summary>
/// <param name="permission">The permission key.</param>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    /// <summary>Gets the permission key.</summary>
    public string Permission { get; } = permission;
}
