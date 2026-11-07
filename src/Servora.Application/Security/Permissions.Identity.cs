using System.ComponentModel;

namespace Servora.Application.Security;

public static partial class Permissions
{
    /// <summary>Permissions of the Identity &amp; Access module.</summary>
    [Description("Identity & Access")]
    public static class Identity
    {
        /// <summary>View users, groups and roles.</summary>
        [Description("View users, groups and roles.")]
        public const string ReadUsers = "admin.users.read";

        /// <summary>Create, edit, deactivate users and reset passwords.</summary>
        [Description("Create, edit, deactivate users and reset passwords.")]
        public const string ManageUsers = "admin.users.manage";

        /// <summary>Create and edit groups and memberships.</summary>
        [Description("Create and edit groups and memberships.")]
        public const string ManageGroups = "admin.groups.manage";

        /// <summary>Create and edit roles and their permissions.</summary>
        [Description("Create and edit roles and their permissions.")]
        public const string ManageRoles = "admin.roles.manage";

        /// <summary>Configure LDAP / Active Directory and Windows authentication.</summary>
        [Description("Configure LDAP / Active Directory and Windows authentication.")]
        public const string ManageDirectory = "admin.directory.manage";

        /// <summary>Configure password, lockout and session policies.</summary>
        [Description("Configure password, lockout and session policies.")]
        public const string ManageAuthPolicies = "admin.auth.manage";
    }
}
