namespace Servora.Application.Ai;

/// <summary>A tool of a sub-agent.</summary>
/// <param name="Name">The tool name.</param>
/// <param name="Title">The title.</param>
/// <param name="RequiresConfirmation">Whether calls need explicit user confirmation.</param>
public sealed record AiAgentToolInfo(string Name, string Title, bool RequiresConfirmation);
