using Servora.Domain.Common;

namespace Servora.Domain.Ai;

/// <summary>Administrator setting of a built-in AI sub-agent. Agents without a setting are enabled.</summary>
public class AiAgentSetting : AuditableEntity
{
    /// <summary>Gets or sets the agent key, for example <c>knowledge</c>.</summary>
    public string AgentKey { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the orchestrator may delegate to the agent.</summary>
    public bool IsEnabled { get; set; } = true;
}
