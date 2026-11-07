namespace Servora.Application.Ai;

/// <summary>A provider-neutral chat message.</summary>
/// <param name="Role">The role.</param>
/// <param name="Content">The text content.</param>
public sealed record AiChatMessage(AiChatRole Role, string Content)
{
    /// <summary>Gets the tool calls of an assistant message.</summary>
    public IReadOnlyList<AiToolCallRequest> ToolCalls { get; init; } = [];

    /// <summary>Gets the call identifier a tool message answers.</summary>
    public string? ToolCallId { get; init; }

    /// <summary>Gets the tool name a tool message answers.</summary>
    public string? ToolName { get; init; }

    /// <summary>Creates a user message.</summary>
    /// <param name="content">The text.</param>
    /// <returns>The message.</returns>
    public static AiChatMessage User(string content) => new(AiChatRole.User, content);

    /// <summary>Creates an assistant message.</summary>
    /// <param name="content">The text.</param>
    /// <param name="toolCalls">The tool calls.</param>
    /// <returns>The message.</returns>
    public static AiChatMessage Assistant(string content, IReadOnlyList<AiToolCallRequest>? toolCalls = null) =>
        new(AiChatRole.Assistant, content) { ToolCalls = toolCalls ?? [] };

    /// <summary>Creates a tool result message.</summary>
    /// <param name="call">The answered call.</param>
    /// <param name="content">The result text.</param>
    /// <returns>The message.</returns>
    public static AiChatMessage ToolResult(AiToolCallRequest call, string content) =>
        new(AiChatRole.Tool, content) { ToolCallId = call.Id, ToolName = call.Name };
}
