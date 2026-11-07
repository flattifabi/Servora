namespace Servora.Application.Mcp;

/// <summary>Catalog entry of an MCP tool for the admin page.</summary>
/// <param name="Name">The tool name.</param>
/// <param name="Title">The title.</param>
/// <param name="Description">The description.</param>
/// <param name="Scope">The key scope required to call the tool.</param>
/// <param name="RequiredPermission">The underlying permission (null when the application service checks it).</param>
/// <param name="IsReadOnly">Whether the tool only reads data.</param>
/// <param name="IsConsequential">Whether the tool performs consequential actions.</param>
/// <param name="IsEnabled">Whether the tool is enabled.</param>
/// <param name="InputSchemaJson">The pretty-printed JSON schema of the arguments.</param>
/// <param name="Calls24h">Calls in the last 24 hours.</param>
public sealed record McpToolInfo(
    string Name,
    string Title,
    string Description,
    string Scope,
    string? RequiredPermission,
    bool IsReadOnly,
    bool IsConsequential,
    bool IsEnabled,
    string InputSchemaJson,
    int Calls24h);
