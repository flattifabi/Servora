namespace Servora.Application.Ai;

/// <summary>A tool call requested by a chat model.</summary>
/// <param name="Id">The provider's call identifier (echoed in the tool result).</param>
/// <param name="Name">The tool name.</param>
/// <param name="ArgumentsJson">The arguments as JSON text.</param>
public sealed record AiToolCallRequest(string Id, string Name, string ArgumentsJson);
