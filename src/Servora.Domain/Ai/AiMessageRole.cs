namespace Servora.Domain.Ai;

/// <summary>Author of an <see cref="AiMessage"/>.</summary>
public enum AiMessageRole
{
    /// <summary>The human user.</summary>
    User = 0,

    /// <summary>The assistant (orchestrator or a sub-agent).</summary>
    Assistant = 1,
}
