namespace Servora.Web.Security;

/// <summary>Naming convention for dynamic permission policies (<c>perm:&lt;key&gt;</c>).</summary>
public static class PermissionPolicy
{
    /// <summary>The policy name prefix.</summary>
    public const string Prefix = "perm:";

    /// <summary>Builds the policy name for a permission.</summary>
    /// <param name="permission">The permission key.</param>
    /// <returns>The policy name.</returns>
    public static string For(string permission) => Prefix + permission;
}
