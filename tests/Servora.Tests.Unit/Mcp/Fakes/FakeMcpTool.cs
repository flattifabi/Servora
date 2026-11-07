using System.Text.Json.Nodes;
using Servora.Application.Common;
using Servora.Application.Mcp;

namespace Servora.Tests.Unit.Mcp.Fakes;

/// <summary>Configurable <see cref="IMcpTool"/> that records its invocations.</summary>
/// <param name="name">The tool name.</param>
/// <param name="requiredPermission">The underlying permission.</param>
/// <param name="consequential">Whether the tool is consequential.</param>
internal sealed class FakeMcpTool(string name, string? requiredPermission = null, bool consequential = false) : IMcpTool
{
    /// <summary>The default schema: a required <c>query</c> string and an optional <c>limit</c> between 1 and 50.</summary>
    public const string DefaultSchema = """
        {
          "type": "object",
          "properties": {
            "query": { "type": "string", "minLength": 1, "maxLength": 100 },
            "limit": { "type": "integer", "minimum": 1, "maximum": 50 }
          },
          "required": ["query"],
          "additionalProperties": false
        }
        """;

    /// <summary>Gets the received argument objects.</summary>
    public List<JsonObject> Calls { get; } = [];

    /// <summary>Gets or sets the schema JSON.</summary>
    public string SchemaJson { get; set; } = DefaultSchema;

    /// <summary>Gets or sets the result factory.</summary>
    public Func<JsonObject, Result<JsonNode>> Handler { get; set; } =
        args => Result<JsonNode>.Success(new JsonObject { ["echo"] = args["query"]?.DeepClone(), ["message"] = "done" });

    /// <inheritdoc />
    public string Name { get; } = name;

    /// <inheritdoc />
    public string Title => $"Fake {Name}";

    /// <inheritdoc />
    public string Description => $"Fake tool {Name} for tests.";

    /// <inheritdoc />
    public string? RequiredPermission { get; } = requiredPermission;

    /// <inheritdoc />
    public bool IsReadOnly => !IsConsequential;

    /// <inheritdoc />
    public bool IsConsequential { get; } = consequential;

    /// <inheritdoc />
    public JsonObject CreateInputSchema() => JsonNode.Parse(SchemaJson)!.AsObject();

    /// <inheritdoc />
    public Task<Result<JsonNode>> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        lock (Calls)
        {
            Calls.Add(arguments);
        }

        return Task.FromResult(Handler(arguments));
    }
}
