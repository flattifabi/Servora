namespace Servora.Application.Ai;

/// <summary>Role of a message sent to a chat model.</summary>
public enum AiChatRole
{
    /// <summary>User (or orchestrator task) input.</summary>
    User = 0,

    /// <summary>Model output, optionally with tool calls.</summary>
    Assistant = 1,

    /// <summary>Result of a tool call.</summary>
    Tool = 2,
}
