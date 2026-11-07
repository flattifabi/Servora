using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Common;

namespace Servora.Application.Analytics;

/// <summary>
/// Decides whether the current actor may read a table for analytics (reports, dashboard sections). Access is granted by
/// the table permission <c>table.&lt;name&gt;.read</c> or by the read permission of the module owning the table.
/// </summary>
public static class TableAccessPolicy
{
    private static readonly Dictionary<string, string> s_modulePermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        [TableNames.Incident] = Permissions.Incidents.Read,
        [TableNames.Problem] = Permissions.Problems.Read,
        [TableNames.ChangeRequest] = Permissions.Changes.Read,
        [TableNames.Request] = Permissions.Requests.Read,
        [TableNames.RequestItem] = Permissions.Requests.Read,
        [TableNames.CatalogTask] = Permissions.Requests.Read,
        [TableNames.Approval] = Permissions.Approvals.Read,
        [TableNames.KnowledgeArticle] = Permissions.Knowledge.Read,
        [TableNames.ConfigurationItem] = Permissions.Cmdb.Read,
        [TableNames.Asset] = Permissions.Assets.Read,
        [TableNames.User] = Permissions.Identity.ReadUsers,
        [TableNames.Group] = Permissions.Identity.ReadUsers,
        [TableNames.Role] = Permissions.Identity.ReadUsers,
        ["task_sla"] = Permissions.Incidents.Read,
    };

    /// <summary>Gets the permissions that grant read access to a table (any of them suffices).</summary>
    /// <param name="tableName">The table name.</param>
    /// <returns>The permission keys.</returns>
    public static IReadOnlyList<string> PermissionsFor(string tableName)
    {
        var result = new List<string> { $"table.{tableName.ToLowerInvariant()}.read" };
        if (s_modulePermissions.TryGetValue(tableName, out var modulePermission))
        {
            result.Add(modulePermission);
        }
        else if (tableName.StartsWith("cmdb_", StringComparison.OrdinalIgnoreCase))
        {
            result.Add(Permissions.Cmdb.Read);
        }
        else if (tableName.StartsWith("kb_", StringComparison.OrdinalIgnoreCase))
        {
            result.Add(Permissions.Knowledge.Read);
        }
        else if (tableName.StartsWith("sc_", StringComparison.OrdinalIgnoreCase))
        {
            result.Add(Permissions.Requests.Read);
        }
        else if (tableName.StartsWith("alm_", StringComparison.OrdinalIgnoreCase))
        {
            result.Add(Permissions.Assets.Read);
        }

        return result;
    }

    /// <summary>Checks read access of the current actor.</summary>
    /// <param name="currentUser">The current actor.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when access is granted.</returns>
    public static async Task<bool> CanReadAsync(ICurrentUser currentUser, string tableName, CancellationToken cancellationToken)
    {
        foreach (var permission in PermissionsFor(tableName))
        {
            if (await currentUser.HasPermissionAsync(permission, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }
}
