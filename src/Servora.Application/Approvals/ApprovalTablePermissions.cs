using Servora.Application.Security;
using Servora.Domain.Changes;
using Servora.Domain.Common;

namespace Servora.Application.Approvals;

/// <summary>Maps approvable tables to the module permissions that allow reading and administering their approvals.</summary>
public static class ApprovalTablePermissions
{
    /// <summary>Gets the permission that allows reading records (and their approvals) of a table.</summary>
    /// <param name="tableName">The technical table name.</param>
    /// <returns>The permission key.</returns>
    public static string ReadPermissionFor(string tableName) => tableName switch
    {
        TableNames.ChangeRequest or ChangeTables.ChangeTask => Permissions.Changes.Read,
        TableNames.Request or TableNames.RequestItem or TableNames.CatalogTask => Permissions.Requests.Read,
        _ => Permissions.Approvals.Read,
    };

    /// <summary>Gets the permission that allows requesting and cancelling approvals for records of a table.</summary>
    /// <param name="tableName">The technical table name.</param>
    /// <returns>The permission key.</returns>
    public static string WritePermissionFor(string tableName) => tableName switch
    {
        TableNames.ChangeRequest or ChangeTables.ChangeTask => Permissions.Changes.Write,
        TableNames.Request or TableNames.RequestItem or TableNames.CatalogTask => Permissions.Requests.Write,
        _ => Permissions.Approvals.Manage,
    };
}
