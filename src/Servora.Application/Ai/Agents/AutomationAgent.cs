using System.Text.Json.Nodes;
using Servora.Application.Security;

namespace Servora.Application.Ai.Agents;

/// <summary>Suggests automation flows and proposes starting them (needs confirmation).</summary>
public sealed class AutomationAgent : AiAgentBase
{
    /// <inheritdoc />
    public override string Key => "automation";

    /// <inheritdoc />
    public override string Name => "Automation Agent";

    /// <inheritdoc />
    public override string Description => "Suggests automation flows for a task, proposes starting them and reports the status of flow runs.";

    /// <inheritdoc />
    public override string Icon => "workflow";

    /// <inheritdoc />
    public override IReadOnlyList<string> Scope { get; } = [Permissions.Automation.Read, Permissions.Automation.Execute];

    /// <inheritdoc />
    public override IReadOnlyList<string> ToolNames { get; } = ["list_flows", "start_flow", "get_flow_run"];

    /// <inheritdoc />
    public override IReadOnlyList<string> Keywords { get; } =
        ["flow", "automate", "automation", "workflow", "run", "start", "trigger", "onboard", "offboard", "provision", "job"];

    /// <inheritdoc />
    public override string SystemPrompt =>
        "You are the Automation Agent of Servora. Use list_flows to find flows that match the task, explain which one fits and why. " +
        "If the user wants to run it, call start_flow with the flow id and the needed inputs (this proposes the run for confirmation). " +
        "Use get_flow_run to report the status of a run." + CommonRules;

    /// <inheritdoc />
    public override (string ToolName, JsonObject Arguments)? BuildPrefetch(string task) => ("list_flows", new JsonObject());
}
