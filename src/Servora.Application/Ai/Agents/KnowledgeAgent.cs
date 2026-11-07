using System.Text.Json.Nodes;
using Servora.Application.Security;

namespace Servora.Application.Ai.Agents;

/// <summary>Answers questions from knowledge base search results.</summary>
public sealed class KnowledgeAgent : AiAgentBase
{
    /// <inheritdoc />
    public override string Key => "knowledge";

    /// <inheritdoc />
    public override string Name => "Knowledge Agent";

    /// <inheritdoc />
    public override string Description => "Answers how-to and troubleshooting questions using the knowledge base and cites the articles it used.";

    /// <inheritdoc />
    public override string Icon => "book-open";

    /// <inheritdoc />
    public override IReadOnlyList<string> Scope { get; } = [Permissions.Knowledge.Read];

    /// <inheritdoc />
    public override IReadOnlyList<string> ToolNames { get; } = ["search_knowledge"];

    /// <inheritdoc />
    public override IReadOnlyList<string> Keywords { get; } =
        ["how", "how to", "what is", "why", "guide", "explain", "knowledge", "article", "kb", "documentation", "reset", "configure", "wie", "anleitung"];

    /// <inheritdoc />
    public override string SystemPrompt =>
        "You are the Knowledge Agent of Servora. Search the knowledge base with search_knowledge (try a second, broader query if the " +
        "first one finds nothing) and answer only from the articles found. Cite the article numbers or titles you used. If nothing " +
        "relevant exists, say so and suggest raising an incident or request." + CommonRules;

    /// <inheritdoc />
    public override (string ToolName, JsonObject Arguments)? BuildPrefetch(string task) =>
        ("search_knowledge", new JsonObject { ["query"] = ToQuery(task), ["limit"] = 5 });
}
