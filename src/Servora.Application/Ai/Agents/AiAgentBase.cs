using System.Text.Json.Nodes;

namespace Servora.Application.Ai.Agents;

/// <summary>Base class of the built-in sub-agents.</summary>
public abstract class AiAgentBase : IAiAgent
{
    /// <summary>Guardrails appended to every agent prompt.</summary>
    protected const string CommonRules =
        "\n\nRules:\n- Only use the provided tools; never invent record numbers, ids or results.\n" +
        "- You act with a restricted identity on behalf of the user. If a tool reports missing permissions, say so plainly.\n" +
        "- Consequential actions (creating records, starting flows, calling data sources) are NOT executed by you: calling such a tool " +
        "proposes the action to the user, who must confirm it in the UI. Never claim that such an action was performed.\n" +
        "- Answer concisely in the language of the task. Markdown (bold, lists, inline code) is allowed.";

    /// <inheritdoc />
    public abstract string Key { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public abstract string Icon { get; }

    /// <inheritdoc />
    public abstract IReadOnlyList<string> Scope { get; }

    /// <inheritdoc />
    public abstract IReadOnlyList<string> ToolNames { get; }

    /// <inheritdoc />
    public abstract string SystemPrompt { get; }

    /// <inheritdoc />
    public abstract IReadOnlyList<string> Keywords { get; }

    /// <inheritdoc />
    public virtual (string ToolName, JsonObject Arguments)? BuildPrefetch(string task) => null;

    /// <summary>Shortens a task to a search query (bounded length).</summary>
    /// <param name="task">The task.</param>
    /// <returns>The query.</returns>
    protected static string ToQuery(string task)
    {
        var query = task.Trim();
        return query.Length <= 200 ? query : query[..200];
    }
}
