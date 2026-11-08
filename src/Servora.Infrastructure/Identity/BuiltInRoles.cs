using Servora.Application.Security;

namespace Servora.Infrastructure.Identity;

/// <summary>Definitions of the built-in system roles and their default permissions.</summary>
public static class BuiltInRoles
{
    /// <summary>Full administrator.</summary>
    public const string Admin = "admin";

    /// <summary>IT service management fulfiller (agent).</summary>
    public const string Itil = "itil";

    /// <summary>Standard employee (self-service).</summary>
    public const string User = "user";

    /// <summary>Owner role of API keys and MCP clients (grants the API and MCP channels, no data access).</summary>
    public const string Integration = "integration_user";

    /// <summary>Configuration administrator (settings, branding, platform studio, portal announcements).</summary>
    public const string ConfigAdmin = "config_admin";

    /// <summary>Security administrator (identity, directory, secrets and API keys).</summary>
    public const string SecurityAdmin = "security_admin";

    /// <summary>Gets all built-in role definitions.</summary>
    public static IReadOnlyList<BuiltInRoleDefinition> All { get; } =
    [
        new(Admin, "System Administrator", "Unrestricted access to every module and administration function.", [Permissions.All]),
        new(Itil, "ITIL Fulfiller", "Service desk and resolver group agent: works incidents, problems, changes, requests and knowledge.",
        [
            Permissions.Portal.Access, Permissions.Incidents.Read, Permissions.Incidents.Create, Permissions.Incidents.Write,
            Permissions.Problems.Read, Permissions.Problems.Create, Permissions.Problems.Write,
            Permissions.Changes.Read, Permissions.Changes.Create, Permissions.Changes.Write,
            Permissions.Requests.Read, Permissions.Requests.Write, Permissions.Approvals.Read,
            Permissions.Knowledge.Read, Permissions.Knowledge.Write, Permissions.Cmdb.Read, Permissions.Assets.Read,
            Permissions.Analytics.Read, Permissions.Platform.ReadRecords, Permissions.Ai.UseAssistant, Permissions.Identity.ReadUsers,
            Permissions.Api.Access,
        ]),
        new(User, "Employee", "Self-service user: portal, own requests and incidents, knowledge base.",
            [Permissions.Portal.Access, Permissions.Knowledge.Read, Permissions.Ai.UseAssistant]),
        new("change_manager", "Change Manager", "Approves and schedules change requests.",
            [Permissions.Changes.Read, Permissions.Changes.Write, Permissions.Changes.Approve, Permissions.Cmdb.Read]),
        new("knowledge_manager", "Knowledge Manager", "Publishes and retires knowledge articles, manages knowledge bases.",
            [Permissions.Knowledge.Read, Permissions.Knowledge.Write, Permissions.Knowledge.Publish, Permissions.Knowledge.Manage]),
        new("catalog_manager", "Catalog Manager", "Maintains the service catalog.",
            [Permissions.Requests.Read, Permissions.Requests.ManageCatalog]),
        new("cmdb_admin", "CMDB Administrator", "Maintains configuration items, classes and relationships.",
            [Permissions.Cmdb.Read, Permissions.Cmdb.Write, Permissions.Cmdb.Manage]),
        new("asset_manager", "Asset Manager", "Maintains hardware and software assets.",
            [Permissions.Assets.Read, Permissions.Assets.Write, Permissions.Assets.Manage, Permissions.Cmdb.Read]),
        new(ConfigAdmin, "Configuration Administrator", "Manages system properties, branding, e-mail, the platform studio and portal announcements.",
        [
            Permissions.Configuration.ManageSettings, Permissions.Platform.ManageTables, Permissions.Platform.ManageBusinessRules,
            Permissions.Platform.ManageSla, Permissions.Platform.ManageNotifications, Permissions.Platform.ReadRecords,
            Permissions.Portal.Manage,
        ]),
        new(SecurityAdmin, "Security Administrator", "Manages users, groups, roles, directory integration, secrets and API keys.",
        [
            Permissions.Identity.ReadUsers, Permissions.Identity.ManageUsers, Permissions.Identity.ManageGroups, Permissions.Identity.ManageRoles,
            Permissions.Identity.ManageDirectory, Permissions.Identity.ManageAuthPolicies, Permissions.Configuration.ManageSecrets,
            Permissions.Api.ManageApiKeys, Permissions.Observability.ReadAudit,

            // A key creator can only grant scopes it holds itself; without these no usable key could be issued.
            Permissions.Api.Access, Permissions.Mcp.Access,
        ]),
        new(Integration, "API & MCP Integration",
            "Lets the user's API keys call the REST API and connect MCP clients (for example Claude Desktop). Grants the channels only: data access still comes from the user's other roles.",
            [Permissions.Api.Access, Permissions.Mcp.Access]),
        new("automation_admin", "Automation Administrator", "Builds and runs flows and schedules.",
            [Permissions.Automation.Read, Permissions.Automation.Manage, Permissions.Automation.Execute, Permissions.Integration.ReadDataSources]),
        new("datasource_admin", "Data Source Administrator", "Manages REST data sources and imports.",
        [
            Permissions.Integration.ReadDataSources, Permissions.Integration.ManageDataSources, Permissions.Integration.ExecuteDataSources,
            Permissions.Integration.ManageImports,
        ]),
        new("auditor", "Auditor", "Read-only access to audit logs, logs, health and analytics.",
        [
            Permissions.Observability.ReadAudit, Permissions.Observability.ReadLogs, Permissions.Observability.ReadHealth,
            Permissions.Analytics.Read, Permissions.Identity.ReadUsers,
        ]),
        new("ai_agent", "AI Agent", "Base role for AI agent identities. Grant additional scoped permissions per agent.",
            [Permissions.Knowledge.Read, Permissions.Incidents.Read]),
    ];
}
