using System.Text.Json.Nodes;

namespace Servora.Application.Ai;

/// <summary>
/// A specialised sub-agent the orchestrator can delegate to. Agents run under their own scoped identity whose
/// permissions are the intersection of <see cref="Scope"/> and the requesting user's permissions (never more).
/// Tools are MCP tools (<see cref="Mcp.IMcpTool"/>) referenced by name. Register as singletons (stateless).
/// </summary>
public interface IAiAgent
{
    /// <summary>Gets the unique key, for example <c>knowledge</c>.</summary>
    string Key { get; }

    /// <summary>Gets the display name.</summary>
    string Name { get; }

    /// <summary>Gets the description (also shown to the orchestrator model).</summary>
    string Description { get; }

    /// <summary>Gets the icon name.</summary>
    string Icon { get; }

    /// <summary>Gets the maximum permissions of the agent identity.</summary>
    IReadOnlyList<string> Scope { get; }

    /// <summary>Gets the names of the tools the agent may use.</summary>
    IReadOnlyList<string> ToolNames { get; }

    /// <summary>Gets the system prompt.</summary>
    string SystemPrompt { get; }

    /// <summary>Gets keywords used to route requests when the provider has no tool calling.</summary>
    IReadOnlyList<string> Keywords { get; }

    /// <summary>
    /// Builds the read-only tool call executed up front when the provider has no tool calling (for example a knowledge
    /// search with the user's question). Returns <see langword="null"/> when nothing should be prefetched.
    /// </summary>
    /// <param name="task">The task text.</param>
    /// <returns>The tool name and arguments, or <see langword="null"/>.</returns>
    (string ToolName, JsonObject Arguments)? BuildPrefetch(string task);
}
