using System.Text.Json;
using System.Text.Json.Nodes;
using Servora.Application.Mcp;

namespace Servora.Web.Components.Api;

/// <summary>Reads the operations of an OpenAPI 3.x JSON document for the API Explorer (schemas resolved one level).</summary>
public static class OpenApiOperationReader
{
    private static readonly string[] s_methods = ["get", "post", "put", "patch", "delete", "head", "options"];
    private static readonly JsonSerializerOptions s_pretty = new() { WriteIndented = true };

    /// <summary>Parses the document.</summary>
    /// <param name="json">The OpenAPI JSON.</param>
    /// <param name="title">The document title.</param>
    /// <param name="version">The document version.</param>
    /// <returns>The operations ordered by tag and path.</returns>
    public static IReadOnlyList<ApiOperation> Read(string json, out string? title, out string? version)
    {
        title = null;
        version = null;
        if (!McpJson.TryParse(json, out var node) || node is not JsonObject root)
        {
            return [];
        }

        title = root["info"]?["title"]?.GetValue<string>();
        version = root["info"]?["version"]?.GetValue<string>();
        var schemas = root["components"]?["schemas"] as JsonObject;
        var operations = new List<ApiOperation>();
        if (root["paths"] is not JsonObject paths)
        {
            return operations;
        }

        foreach (var (path, pathNode) in paths)
        {
            if (pathNode is not JsonObject pathItem)
            {
                continue;
            }

            foreach (var method in s_methods)
            {
                if (pathItem[method] is not JsonObject operation)
                {
                    continue;
                }

                var parameters = (operation["parameters"] as JsonArray ?? new JsonArray())
                    .Concat(pathItem["parameters"] as JsonArray ?? new JsonArray())
                    .OfType<JsonObject>()
                    .Select(p => new ApiOperationParameter(
                        Text(p["name"]) ?? "?",
                        Text(p["in"]) ?? "query",
                        p["required"]?.GetValueKind() == JsonValueKind.True,
                        TypeOf(p["schema"] as JsonObject, schemas),
                        Text(p["description"])))
                    .ToList();

                string? requestType = null;
                string? requestSchema = null;
                if (operation["requestBody"]?["content"] is JsonObject requestContent && requestContent.Count > 0)
                {
                    var first = requestContent.First();
                    requestType = first.Key;
                    requestSchema = SchemaJson(first.Value?["schema"] as JsonObject, schemas);
                }

                var responses = new List<ApiOperationResponse>();
                if (operation["responses"] is JsonObject responseMap)
                {
                    foreach (var (status, responseNode) in responseMap)
                    {
                        var content = responseNode?["content"] as JsonObject;
                        var firstContent = content?.FirstOrDefault();
                        responses.Add(new ApiOperationResponse(
                            status,
                            Text(responseNode?["description"]),
                            firstContent?.Key,
                            SchemaJson(firstContent?.Value?["schema"] as JsonObject, schemas)));
                    }
                }

                operations.Add(new ApiOperation
                {
                    Method = method.ToUpperInvariant(),
                    Path = path,
                    Tag = (operation["tags"] as JsonArray)?.FirstOrDefault()?.GetValue<string>() ?? "General",
                    OperationId = Text(operation["operationId"]),
                    Summary = Text(operation["summary"]),
                    Description = Text(operation["description"]),
                    Parameters = parameters,
                    RequestContentType = requestType,
                    RequestSchemaJson = requestSchema,
                    Responses = responses,
                    IsAnonymous = operation["security"] is JsonArray { Count: 0 } || path.EndsWith("/health", StringComparison.OrdinalIgnoreCase),
                });
            }
        }

        return operations.OrderBy(o => o.Tag).ThenBy(o => o.Path).ThenBy(o => Array.IndexOf(s_methods, o.Method.ToLowerInvariant())).ToList();
    }

    private static string? Text(JsonNode? node) => node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

    private static JsonObject? Resolve(JsonObject? schema, JsonObject? schemas)
    {
        if (schema?["$ref"] is JsonValue reference && Text(reference) is { } refText && refText.StartsWith("#/components/schemas/", StringComparison.Ordinal))
        {
            return schemas?[refText["#/components/schemas/".Length..]] as JsonObject;
        }

        return schema;
    }

    private static string TypeOf(JsonObject? schema, JsonObject? schemas)
    {
        var resolved = Resolve(schema, schemas);
        if (resolved is null)
        {
            return "string";
        }

        var type = resolved["type"] switch
        {
            JsonValue v => Text(v),
            JsonArray a => string.Join(" | ", a.Select(Text).OfType<string>()),
            _ => null,
        } ?? "object";
        return Text(resolved["format"]) is { } format ? $"{type} ({format})" : type;
    }

    private static string? SchemaJson(JsonObject? schema, JsonObject? schemas)
    {
        var resolved = Resolve(schema, schemas);
        if (resolved is null)
        {
            return null;
        }

        // Inline referenced property schemas one level deep for readability.
        var copy = resolved.DeepClone().AsObject();
        if (copy["properties"] is JsonObject properties)
        {
            foreach (var name in properties.Select(p => p.Key).ToList())
            {
                if (properties[name] is JsonObject property && Resolve(property, schemas) is { } inner && !ReferenceEquals(inner, property))
                {
                    properties[name] = inner.DeepClone();
                }
            }
        }

        return copy.ToJsonString(s_pretty);
    }
}
