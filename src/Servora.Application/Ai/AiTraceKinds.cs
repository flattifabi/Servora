namespace Servora.Application.Ai;

/// <summary>Kinds of execution trace steps.</summary>
public static class AiTraceKinds
{
    /// <summary>The orchestrator delegated a task to a sub-agent.</summary>
    public const string Delegation = "delegation";

    /// <summary>A sub-agent called a tool.</summary>
    public const string Tool = "tool";

    /// <summary>A sub-agent proposed a consequential action that needs user confirmation.</summary>
    public const string Action = "action";
}
