using System.Text.Json.Nodes;
using Servora.Application.Mcp;

namespace Servora.Tests.Unit.Mcp;

/// <summary>Tests for the dependency-free <see cref="JsonSchemaValidator"/> used for MCP tool inputs.</summary>
public sealed class JsonSchemaValidatorTests
{
    private static readonly JsonObject s_schema = JsonNode.Parse("""
        {
          "type": "object",
          "properties": {
            "table": { "type": "string", "pattern": "^[a-z_]+$", "maxLength": 20 },
            "id": { "type": "string", "format": "uuid" },
            "when": { "type": "string", "format": "date-time" },
            "limit": { "type": "integer", "minimum": 1, "maximum": 50 },
            "state": { "type": "string", "enum": ["new", "closed"] },
            "note": { "type": ["string", "null"] },
            "fields": { "type": "array", "items": { "type": "string" }, "minItems": 1, "maxItems": 3 },
            "values": { "type": "object", "maxProperties": 2 }
          },
          "required": ["table"],
          "additionalProperties": false
        }
        """)!.AsObject();

    [Fact]
    public void ValidInput_HasNoErrors()
    {
        var value = JsonNode.Parse("""
            { "table": "incident", "id": "0f8fad5b-d9cb-469f-a165-70867728950e", "when": "2026-11-07T10:00:00Z",
              "limit": 10, "state": "new", "note": null, "fields": ["number"], "values": { "a": "b" } }
            """);

        Assert.Empty(JsonSchemaValidator.Validate(value, s_schema));
    }

    [Theory]
    [InlineData("""{ }""", "table")]
    [InlineData("""{ "table": 5 }""", "expected string")]
    [InlineData("""{ "table": "Incident!" }""", "pattern")]
    [InlineData("""{ "table": "aaaaaaaaaaaaaaaaaaaaaaaaa" }""", "table")]
    [InlineData("""{ "table": "incident", "id": "nope" }""", "id")]
    [InlineData("""{ "table": "incident", "when": "yesterday" }""", "when")]
    [InlineData("""{ "table": "incident", "limit": 0 }""", "limit")]
    [InlineData("""{ "table": "incident", "limit": 1.5 }""", "limit")]
    [InlineData("""{ "table": "incident", "state": "open" }""", "state")]
    [InlineData("""{ "table": "incident", "fields": [] }""", "fields")]
    [InlineData("""{ "table": "incident", "fields": ["a", 1] }""", "fields")]
    [InlineData("""{ "table": "incident", "values": { "a": 1, "b": 2, "c": 3 } }""", "values")]
    [InlineData("""{ "table": "incident", "extra": true }""", "extra")]
    public void InvalidInput_IsReportedWithThePath(string json, string expectedFragment)
    {
        var errors = JsonSchemaValidator.Validate(JsonNode.Parse(json), s_schema);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains(expectedFragment, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RootTypeMismatch_IsReported()
    {
        Assert.NotEmpty(JsonSchemaValidator.Validate(new JsonArray(), s_schema));
        Assert.NotEmpty(JsonSchemaValidator.Validate(null, s_schema));
    }

    [Fact]
    public void ErrorCount_IsBounded()
    {
        var wide = new JsonObject();
        for (var i = 0; i < 100; i++)
        {
            wide[$"x{i}"] = i;
        }

        var errors = JsonSchemaValidator.Validate(wide, s_schema);

        Assert.InRange(errors.Count, 1, 20);
    }
}
