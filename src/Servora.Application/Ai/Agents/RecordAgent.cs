using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Servora.Application.Security;

namespace Servora.Application.Ai.Agents;

/// <summary>Looks up records (incidents, problems, changes, requests, CIs, users) with read-only tools.</summary>
public sealed partial class RecordAgent : AiAgentBase
{
    /// <inheritdoc />
    public override string Key => "records";

    /// <inheritdoc />
    public override string Name => "Record Agent";

    /// <inheritdoc />
    public override string Description =>
        "Finds and summarises records such as incidents, problems, changes, requests, configuration items and users (read-only).";

    /// <inheritdoc />
    public override string Icon => "search";

    /// <inheritdoc />
    /// <remarks><c>table.*</c> covers custom tables; the agent's tools are read-only, so no write access results from it.</remarks>
    public override IReadOnlyList<string> Scope { get; } =
    [
        Permissions.Platform.ReadRecords, Permissions.Incidents.Read, Permissions.Problems.Read, Permissions.Changes.Read,
        Permissions.Requests.Read, Permissions.Cmdb.Read, Permissions.Assets.Read, Permissions.Knowledge.Read,
        Permissions.Identity.ReadUsers, "table.*",
    ];

    /// <inheritdoc />
    public override IReadOnlyList<string> ToolNames { get; } = ["search_records", "get_record"];

    /// <inheritdoc />
    public override IReadOnlyList<string> Keywords { get; } =
        ["find", "show", "list", "status of", "look up", "lookup", "which", "open incidents", "my incidents", "inc", "prb", "chg", "req", "ci ", "server", "user", "zeige", "suche"];

    /// <inheritdoc />
    public override string SystemPrompt =>
        "You are the Record Agent of Servora. Use search_records (tables: incident, problem, change_request, sc_request, sc_req_item, " +
        "cmdb_ci, alm_asset, kb_knowledge, sys_user, sys_user_group) and get_record to find the requested information. Summarise the " +
        "results as a short list with numbers, short descriptions, states and priorities. Do not dump raw JSON." + CommonRules;

    /// <inheritdoc />
    public override (string ToolName, JsonObject Arguments)? BuildPrefetch(string task)
    {
        var number = RecordNumber().Match(task);
        var table = number.Success
            ? number.Groups["prefix"].Value.ToUpperInvariant() switch
            {
                "PRB" => "problem",
                "CHG" => "change_request",
                "REQ" => "sc_request",
                "RITM" => "sc_req_item",
                _ => "incident",
            }
            : "incident";
        return ("search_records", new JsonObject { ["table"] = table, ["query"] = number.Success ? number.Value : ToQuery(task), ["limit"] = 10 });
    }

    [GeneratedRegex(@"\b(?<prefix>INC|PRB|CHG|REQ|RITM)\d{4,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RecordNumber();
}
