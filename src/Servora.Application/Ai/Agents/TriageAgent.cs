using Servora.Application.Security;

namespace Servora.Application.Ai.Agents;

/// <summary>Classifies and prioritises incident descriptions and can propose creating an incident (needs confirmation).</summary>
public sealed class TriageAgent : AiAgentBase
{
    /// <inheritdoc />
    public override string Key => "triage";

    /// <inheritdoc />
    public override string Name => "Triage Agent";

    /// <inheritdoc />
    public override string Description =>
        "Classifies and prioritises a described IT problem (category, impact, urgency, priority, suggested assignment), checks for similar " +
        "open incidents and can propose creating an incident.";

    /// <inheritdoc />
    public override string Icon => "target";

    /// <inheritdoc />
    public override IReadOnlyList<string> Scope { get; } = [Permissions.Incidents.Read, Permissions.Incidents.Create, Permissions.Platform.ReadRecords];

    /// <inheritdoc />
    public override IReadOnlyList<string> ToolNames { get; } = ["search_records", "create_incident"];

    /// <inheritdoc />
    public override IReadOnlyList<string> Keywords { get; } =
        ["broken", "not working", "error", "outage", "down", "crash", "triage", "priority", "urgent", "incident", "report", "fails", "slow", "kaputt", "störung", "fehler"];

    /// <inheritdoc />
    public override string SystemPrompt =>
        "You are the Triage Agent of Servora, an IT service management platform. Analyse the described problem and produce: " +
        "category, impact (1 high, 2 medium, 3 low), urgency (1-3), resulting priority (P1-P5) and a short justification. " +
        "Use search_records on table 'incident' to look for similar open incidents. If the user wants an incident to be created, " +
        "call create_incident with a precise short_description, description, impact and urgency." + CommonRules;
}
