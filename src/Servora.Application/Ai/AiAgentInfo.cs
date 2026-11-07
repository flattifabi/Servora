namespace Servora.Application.Ai;

/// <summary>Sub-agent as shown in the admin UI and the assistant.</summary>
/// <param name="Key">The key.</param>
/// <param name="Name">The name.</param>
/// <param name="Description">The description.</param>
/// <param name="Icon">The icon.</param>
/// <param name="Scope">The maximum permissions.</param>
/// <param name="Tools">The tool names (with a flag for consequential tools).</param>
/// <param name="IsEnabled">Whether the agent is enabled.</param>
public sealed record AiAgentInfo(string Key, string Name, string Description, string Icon, IReadOnlyList<string> Scope, IReadOnlyList<AiAgentToolInfo> Tools, bool IsEnabled);
