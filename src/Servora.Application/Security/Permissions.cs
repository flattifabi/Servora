namespace Servora.Application.Security;

/// <summary>
/// Root of the permission key catalog. Each module contributes a nested static class in its own file
/// (<c>Permissions.&lt;Module&gt;.cs</c>). Every <c>public const string</c> decorated with
/// <see cref="System.ComponentModel.DescriptionAttribute"/> is discovered by <see cref="PermissionCatalog"/>.
/// </summary>
/// <remarks>Permission keys are lower case, dot separated: <c>&lt;area&gt;.&lt;resource&gt;.&lt;action&gt;</c>.</remarks>
public static partial class Permissions
{
    /// <summary>Wildcard that grants every permission. Only the built-in <c>admin</c> role holds it.</summary>
    public const string All = "*";
}
