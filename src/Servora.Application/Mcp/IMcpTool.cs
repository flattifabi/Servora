using System.Text.Json.Nodes;
using Servora.Application.Common;

namespace Servora.Application.Mcp;

/// <summary>
/// A tool exposed through the MCP server and usable by AI sub-agents. Tools are thin adapters over application
/// services (which enforce permissions again for the current actor). Register implementations as scoped services.
/// </summary>
/// <remarks>
/// Rules: no arbitrary SQL, URL or code execution; inputs are validated against <see cref="CreateInputSchema"/> before
/// <see cref="ExecuteAsync"/> is called; outputs must be bounded.
/// </remarks>
public interface IMcpTool
{
    /// <summary>Gets the unique tool name (<c>lower_snake_case</c>).</summary>
    string Name { get; }

    /// <summary>Gets the human readable title.</summary>
    string Title { get; }

    /// <summary>Gets the description for the model.</summary>
    string Description { get; }

    /// <summary>Gets the underlying permission that the caller must hold, or <see langword="null"/> when the called application service checks it.</summary>
    string? RequiredPermission { get; }

    /// <summary>Gets a value indicating whether the tool only reads data.</summary>
    bool IsReadOnly { get; }

    /// <summary>Gets a value indicating whether the tool performs a consequential action (AI agents need explicit user confirmation).</summary>
    bool IsConsequential { get; }

    /// <summary>Creates a new copy of the JSON schema of the tool arguments.</summary>
    /// <returns>The JSON schema object.</returns>
    JsonObject CreateInputSchema();

    /// <summary>Executes the tool with validated arguments.</summary>
    /// <param name="arguments">The arguments (already validated against the schema).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The structured result, or a failure with a user-safe message.</returns>
    Task<Result<JsonNode>> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken);
}
