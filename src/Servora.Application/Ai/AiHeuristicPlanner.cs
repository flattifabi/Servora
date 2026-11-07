namespace Servora.Application.Ai;

/// <summary>Keyword-based routing used when the provider does not support tool calling.</summary>
public static class AiHeuristicPlanner
{
    /// <summary>Chooses the best matching agent for a message.</summary>
    /// <param name="message">The user message.</param>
    /// <param name="agents">The enabled agents.</param>
    /// <returns>The agent, or <see langword="null"/> when no agent matches (the orchestrator answers directly).</returns>
    public static IAiAgent? Choose(string message, IReadOnlyList<IAiAgent> agents)
    {
        var text = $" {message.ToLowerInvariant()} ";
        IAiAgent? best = null;
        var bestScore = 0;
        foreach (var agent in agents)
        {
            var score = agent.Keywords.Count(k => text.Contains(k, StringComparison.Ordinal));
            if (score > bestScore)
            {
                best = agent;
                bestScore = score;
            }
        }

        return best;
    }
}
