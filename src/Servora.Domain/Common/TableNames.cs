namespace Servora.Domain.Common;

/// <summary>
/// Well-known technical table names. Use these constants instead of string literals when referencing core tables.
/// </summary>
public static class TableNames
{
    /// <summary>Users.</summary>
    public const string User = "sys_user";

    /// <summary>Groups (assignment and security groups).</summary>
    public const string Group = "sys_user_group";

    /// <summary>Roles.</summary>
    public const string Role = "sys_role";

    /// <summary>Incidents.</summary>
    public const string Incident = "incident";

    /// <summary>Problems.</summary>
    public const string Problem = "problem";

    /// <summary>Change requests.</summary>
    public const string ChangeRequest = "change_request";

    /// <summary>Service catalog requests.</summary>
    public const string Request = "sc_request";

    /// <summary>Requested items (one per ordered catalog item).</summary>
    public const string RequestItem = "sc_req_item";

    /// <summary>Catalog fulfilment tasks.</summary>
    public const string CatalogTask = "sc_task";

    /// <summary>Approvals.</summary>
    public const string Approval = "sys_approval";

    /// <summary>Knowledge articles.</summary>
    public const string KnowledgeArticle = "kb_knowledge";

    /// <summary>Configuration items.</summary>
    public const string ConfigurationItem = "cmdb_ci";

    /// <summary>Assets.</summary>
    public const string Asset = "alm_asset";
}
