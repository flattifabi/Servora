using Servora.Application.Security;

namespace Servora.Web.Navigation;

/// <summary>
/// Central application navigation. Every module page that should appear in the sidebar / command palette is listed
/// here with the permission that is required to see it. The page itself must still enforce the permission.
/// </summary>
public static class NavigationCatalog
{
    /// <summary>Gets the navigation sections in display order.</summary>
    public static IReadOnlyList<NavSection> Sections { get; } =
    [
        new("Workspace",
        [
            new("My Work", "/", "home", null, "dashboard overview start"),
            new("Self-Service Portal", "/portal", "life-buoy", Permissions.Portal.Access, "portal help request catalog"),
            new("AI Assistant", "/assistant", "sparkles", Permissions.Ai.UseAssistant, "ai chat copilot agent"),
            new("My Approvals", "/approvals", "check-square", null, "approve reject"),
            new("Notifications", "/notifications", "inbox", null, "inbox alerts messages unread"),
        ]),
        new("Service Management",
        [
            new("Incidents", "/incidents", "alert-triangle", Permissions.Incidents.Read, "inc ticket outage"),
            new("Problems", "/problems", "lightbulb", Permissions.Problems.Read, "prb known error root cause"),
            new("Changes", "/changes", "git-pull-request", Permissions.Changes.Read, "chg cab release"),
            new("Requests", "/requests", "shopping-cart", Permissions.Requests.Read, "req ritm fulfilment"),
            new("New Request", "/requests/new", "plus", Permissions.Requests.Write, "order on behalf catalog request"),
            new("Service Catalog", "/catalog-admin", "package", Permissions.Requests.ManageCatalog, "catalog items variables"),
        ]),
        new("Knowledge & Assets",
        [
            new("Knowledge Base", "/knowledge", "book-open", Permissions.Knowledge.Read, "kb home search"),
            new("Articles", "/knowledge/articles", "file-text", Permissions.Knowledge.Read, "kb articles docs drafts review"),
            new("Knowledge Admin", "/knowledge/admin", "folder", Permissions.Knowledge.Manage, "knowledge bases categories"),
            new("CMDB", "/cmdb", "server", Permissions.Cmdb.Read, "configuration items ci relationships"),
            new("Service Map", "/cmdb/map", "network", Permissions.Cmdb.Read, "dependency graph topology"),
            new("CI Classes", "/cmdb/classes", "layers", Permissions.Cmdb.Read, "ci class attributes schema"),
            new("Assets", "/assets", "box", Permissions.Assets.Read, "hardware software inventory"),
        ]),
        new("Automation",
        [
            new("Flow Designer", "/automation/flows", "workflow", Permissions.Automation.Read, "flows automation workflow"),
            new("Executions", "/automation/runs", "activity", Permissions.Automation.Read, "runs history"),
            new("Schedules", "/automation/schedules", "calendar-clock", Permissions.Automation.Read, "cron jobs scheduled"),
            new("Data Sources", "/integration/datasources", "plug", Permissions.Integration.ReadDataSources, "rest api http integration"),
            new("Import Sets", "/integration/imports", "download", Permissions.Integration.ManageImports, "import transform map"),
        ]),
        new("Analytics",
        [
            new("Dashboards", "/analytics", "pie-chart", Permissions.Analytics.Read, "kpi metrics"),
            new("Reports", "/analytics/reports", "bar-chart", Permissions.Analytics.Read, "report builder chart"),
        ]),
        new("Platform Studio",
        [
            new("Tables & Fields", "/admin/tables", "table", Permissions.Platform.ManageTables, "custom tables dictionary forms"),
            new("Business Rules", "/admin/business-rules", "zap", Permissions.Platform.ManageBusinessRules, "rules triggers"),
            new("SLA Definitions", "/admin/sla", "timer", Permissions.Platform.ManageSla, "service level agreement"),
            new("Notifications", "/admin/notifications", "mail", Permissions.Platform.ManageNotifications, "email templates"),
        ]),
        new("Administration",
        [
            new("Users", "/admin/users", "user", Permissions.Identity.ReadUsers, "accounts people"),
            new("Groups", "/admin/groups", "users", Permissions.Identity.ReadUsers, "teams assignment groups"),
            new("Roles & Permissions", "/admin/roles", "shield", Permissions.Identity.ManageRoles, "rbac acl"),
            new("Directory & SSO", "/admin/directory", "id-card", Permissions.Identity.ManageDirectory, "ldap active directory ad windows kerberos"),
            new("Authentication Policy", "/admin/auth", "lock", Permissions.Identity.ManageAuthPolicies, "password lockout session"),
            new("System Settings", "/admin/settings", "sliders", Permissions.Configuration.ManageSettings, "properties branding smtp email"),
            new("Mail Outbox", "/admin/settings/outbox", "send", Permissions.Configuration.ManageSettings, "email queue delivery retry smtp"),
            new("Portal Announcements", "/portal/admin/announcements", "bell", Permissions.Portal.Manage, "portal banner news"),
            new("Portal Settings", "/portal/admin/settings", "sliders", Permissions.Portal.Manage, "portal hero support footer links branding"),
            new("Secrets", "/admin/secrets", "key", Permissions.Configuration.ManageSecrets, "credentials vault"),
            new("API Keys", "/admin/api-keys", "braces", Permissions.Api.ManageApiKeys, "tokens rest"),
            new("MCP Server", "/admin/mcp", "bot", Permissions.Mcp.Manage, "model context protocol tools"),
            new("AI & Agents", "/admin/ai", "wand", Permissions.Ai.Manage, "llm provider orchestrator agents"),
        ]),
        new("Operations",
        [
            new("Logs", "/admin/logs", "terminal", Permissions.Observability.ReadLogs, "logging debug"),
            new("Audit Log", "/admin/audit", "history", Permissions.Observability.ReadAudit, "audit trail security"),
            new("System Health", "/admin/health", "heart-pulse", Permissions.Observability.ReadHealth, "status metrics database"),
            new("Backup & Restore", "/admin/backups", "hard-drive", Permissions.Backup.Read, "backup restore rollback snapshot schedule migration"),
            new("API Explorer", "/admin/api-docs", "code", Permissions.Api.Access, "openapi swagger rest docs"),
        ]),
    ];

    /// <summary>Gets all entries flattened.</summary>
    public static IEnumerable<NavEntry> AllEntries => Sections.SelectMany(s => s.Entries);
}
