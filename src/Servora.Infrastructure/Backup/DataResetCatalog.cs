namespace Servora.Infrastructure.Backup;

/// <summary>
/// Classifies every table for the data reset. New tables must be added to one of the lists; a unit test fails until
/// they are, so that a reset never silently keeps new records or removes new configuration.
/// </summary>
public static class DataResetCatalog
{
    /// <summary>The display name of the area that also covers the removed user accounts.</summary>
    public const string UsersArea = "Users and groups";

    /// <summary>Gets the areas whose tables are emptied, children before parents across the whole list.</summary>
    public static IReadOnlyList<(string Area, string[] Tables)> Cleared { get; } =
    [
        ("Incidents, problems and changes", ["task_sla", "incident", "problem", "change_task", "change_request"]),
        ("Requests and service catalog", ["sc_task", "sc_req_item", "sc_request", "sc_favorite", "item_option_new", "sc_item_section", "sc_cat_item", "sc_category"]),
        ("Knowledge", ["kb_feedback", "kb_m2m_ci", "kb_version", "kb_knowledge", "kb_category", "kb_knowledge_base"]),
        ("Assets and CMDB", ["alm_asset", "cmdb_rel_ci", "cmdb_ci"]),
        ("Portal announcements", ["portal_announcement"]),
        ("Automation and integrations", ["automation_flow_run_step", "automation_flow_run", "automation_flow", "int_data_source_log", "int_data_source_endpoint", "int_data_source", "int_import_row", "int_import_run", "int_import_set"]),
        ("Comments, attachments, approvals and notifications", ["sys_journal", "sys_attachment", "sys_approval", "sys_notification_inbox", "sys_email_outbox"]),
        ("Records of custom tables", ["sys_record_field_value", "sys_custom_record"]),
        ("AI conversations", ["ai_tool_call", "ai_message", "ai_conversation"]),
        (UsersArea, ["sys_user_grmember", "sys_group_has_role", "sys_user_group"]),
    ];

    /// <summary>
    /// Gets the tables a reset leaves alone: configuration, the audit trail and the tables that are only trimmed
    /// (user accounts and their roles and pins) or rewound (number sequences).
    /// </summary>
    public static IReadOnlyList<string> Kept { get; } =
    [
        "ai_agent", "ai_provider", "cmdb_class", "contract_sla", "incident_category", "int_network_allow_list",
        "sys_api_key", "sys_api_key_usage", "sys_audit_event", "sys_dashboard_pin", "sys_db_object", "sys_dictionary",
        "sys_directory_group_map", "sys_directory_sync_run", "sys_mcp_invocation", "sys_mcp_tool", "sys_number",
        "sys_properties", "sys_report", "sys_role", "sys_role_permission", "sys_script", "sys_secret", "sys_ui_layout",
        "sys_user", "sys_user_has_role", "sysevent_email_action", "sysevent_email_template",
    ];
}
