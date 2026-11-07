using Microsoft.AspNetCore.Authorization;

namespace Servora.Web.Security;

/// <summary>
/// Requires an authenticated principal holding a permission. Usable on Razor pages (<c>@attribute [RequirePermission(...)]</c>)
/// and minimal API endpoints (<c>.RequireAuthorization(PermissionPolicy.For(...))</c>).
/// </summary>
/// <param name="permission">The permission key, for example <c>incident.write</c>.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(string permission) : AuthorizeAttribute(PermissionPolicy.For(permission))
{
    /// <summary>Gets the required permission key.</summary>
    public string Permission { get; } = permission;
}
