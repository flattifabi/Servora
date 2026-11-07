using Servora.Application.Security;
using Servora.Domain.Common;

namespace Servora.Application.Tables;

/// <summary>
/// Maps typed tables to the permissions of their owning modules. Tables that are not listed (custom tables and typed
/// tables of future modules) use the generic keys <c>table.&lt;name&gt;.read|create|write|delete</c>, which are also
/// accepted as an alternative for listed tables whose operation is generically available.
/// </summary>
public static class TablePolicies
{
    private static readonly Dictionary<string, TablePolicy> s_policies = new(StringComparer.OrdinalIgnoreCase)
    {
        // Identity tables: user creation, deletion and every security relevant attribute stay in the identity module
        // (password handling, security stamps, privilege-escalation guard). Generic access is limited to profile data.
        [TableNames.User] = new(Permissions.Identity.ReadUsers, null, Permissions.Identity.ManageUsers, null)
        {
            WritableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "DisplayName", "FirstName", "LastName", "Email", "Phone", "Title", "Department", "Location", "ManagerId", "TimeZone",
            },
        },
        [TableNames.Group] = new(Permissions.Identity.ReadUsers, Permissions.Identity.ManageGroups, Permissions.Identity.ManageGroups, Permissions.Identity.ManageGroups)
        {
            WritableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Name", "Description", "Email", "Type", "ManagerId", "ParentId", "IsActive",
            },
        },
        [TableNames.Role] = new(Permissions.Identity.ReadUsers, null, null, null),
        [TableNames.Approval] = new(Permissions.Approvals.Read, null, null, null),
        [TableNames.ConfigurationItem] = new(Permissions.Cmdb.Read, Permissions.Cmdb.Write, Permissions.Cmdb.Write, Permissions.Cmdb.Manage),
        [TableNames.Incident] = new(Permissions.Incidents.Read, Permissions.Incidents.Create, Permissions.Incidents.Write, Permissions.Incidents.Delete) { NumberPrefix = "INC" },
        [TableNames.Problem] = new(Permissions.Problems.Read, Permissions.Problems.Create, Permissions.Problems.Write, Permissions.Problems.Delete) { NumberPrefix = "PRB" },
        [TableNames.ChangeRequest] = new(Permissions.Changes.Read, Permissions.Changes.Create, Permissions.Changes.Write, Permissions.Changes.Delete) { NumberPrefix = "CHG" },
        [TableNames.Request] = new(Permissions.Requests.Read, Permissions.Requests.Write, Permissions.Requests.Write, Permissions.Requests.ManageCatalog) { NumberPrefix = "REQ" },
        [TableNames.RequestItem] = new(Permissions.Requests.Read, Permissions.Requests.Write, Permissions.Requests.Write, Permissions.Requests.ManageCatalog) { NumberPrefix = "RITM" },
        [TableNames.CatalogTask] = new(Permissions.Requests.Read, Permissions.Requests.Write, Permissions.Requests.Write, Permissions.Requests.ManageCatalog) { NumberPrefix = "SCTASK" },
        [TableNames.KnowledgeArticle] = new(Permissions.Knowledge.Read, Permissions.Knowledge.Write, Permissions.Knowledge.Write, Permissions.Knowledge.Manage) { NumberPrefix = "KB" },
        [TableNames.Asset] = new(Permissions.Assets.Read, Permissions.Assets.Write, Permissions.Assets.Write, Permissions.Assets.Manage) { NumberPrefix = "AST" },
    };

    /// <summary>Gets the well-known record number prefixes of typed tables (used by <c>/go/{number}</c>).</summary>
    public static IReadOnlyDictionary<string, string> NumberPrefixes { get; } = s_policies
        .Where(p => p.Value.NumberPrefix is not null)
        .ToDictionary(p => p.Value.NumberPrefix!, p => p.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>Finds the policy of a typed table.</summary>
    /// <param name="tableName">The technical table name.</param>
    /// <returns>The policy or <see langword="null"/> when the table uses the generic <c>table.*</c> keys.</returns>
    public static TablePolicy? Find(string tableName) => s_policies.GetValueOrDefault(tableName);

    /// <summary>Builds the generic permission key of a table operation, for example <c>table.u_asset_audit.read</c>.</summary>
    /// <param name="tableName">The technical table name.</param>
    /// <param name="operation">The operation.</param>
    /// <returns>The permission key.</returns>
    public static string GenericPermission(string tableName, TableOperation operation) =>
        $"table.{tableName.ToLowerInvariant()}.{operation.ToString().ToLowerInvariant()}";
}
