using System.Text.Json.Nodes;

namespace Servora.Application.Ai;

/// <summary>A tool offered to a chat model.</summary>
/// <param name="Name">The tool name.</param>
/// <param name="Description">The description.</param>
/// <param name="Parameters">The JSON schema of the arguments.</param>
public sealed record AiToolDefinition(string Name, string Description, JsonObject Parameters);
