using System.Text.Json.Nodes;
using Servora.Application.Common;

namespace Servora.Application.Mcp.Tools;

/// <summary>Base class of the built-in MCP tools: schema from a JSON literal plus common result helpers.</summary>
public abstract class McpToolBase : IMcpTool
{
    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Title { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public abstract string? RequiredPermission { get; }

    /// <inheritdoc />
    public virtual bool IsReadOnly => true;

    /// <inheritdoc />
    public virtual bool IsConsequential => false;

    /// <summary>Gets the JSON schema of the arguments as a JSON literal.</summary>
    protected abstract string SchemaJson { get; }

    /// <inheritdoc />
    public JsonObject CreateInputSchema() => JsonNode.Parse(SchemaJson)!.AsObject();

    /// <inheritdoc />
    public abstract Task<Result<JsonNode>> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken);

    /// <summary>Wraps a value in a successful result.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The result.</returns>
    protected static Result<JsonNode> Ok<T>(T value) => Result<JsonNode>.Success(McpJson.ToNode(value));

    /// <summary>Converts a failed result.</summary>
    /// <param name="failure">The failure.</param>
    /// <returns>The result.</returns>
    protected static Result<JsonNode> Fail(Result failure) => Result<JsonNode>.From(failure);
}
