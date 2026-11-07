using System.Text.Json;
using System.Text.Json.Nodes;
using Servora.Application.Ai;
using Servora.Application.Mcp;

namespace Servora.Infrastructure.Ai;

/// <summary>Request / response mapping of the Anthropic Messages API.</summary>
public static class AnthropicWireFormat
{
    /// <summary>The API version header value.</summary>
    public const string ApiVersion = "2023-06-01";

    /// <summary>Builds the request body. Consecutive messages of the same role are merged (the API expects alternation).</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="request">The request.</param>
    /// <returns>The body.</returns>
    public static JsonObject BuildBody(AiProviderConnection connection, AiChatRequest request)
    {
        var messages = new JsonArray();
        string? currentRole = null;
        JsonArray? currentContent = null;

        void Append(string role, JsonNode block)
        {
            if (role != currentRole || currentContent is null)
            {
                currentContent = [];
                messages.Add(new JsonObject { ["role"] = role, ["content"] = currentContent });
                currentRole = role;
            }

            currentContent.Add(block);
        }

        foreach (var message in request.Messages)
        {
            switch (message.Role)
            {
                case AiChatRole.User:
                    Append("user", new JsonObject { ["type"] = "text", ["text"] = string.IsNullOrEmpty(message.Content) ? "(empty)" : message.Content });
                    break;
                case AiChatRole.Assistant:
                    if (!string.IsNullOrEmpty(message.Content))
                    {
                        Append("assistant", new JsonObject { ["type"] = "text", ["text"] = message.Content });
                    }

                    foreach (var call in message.ToolCalls)
                    {
                        var input = McpJson.TryParse(call.ArgumentsJson, out var parsed) && parsed is JsonObject o ? o : new JsonObject();
                        Append("assistant", new JsonObject { ["type"] = "tool_use", ["id"] = call.Id, ["name"] = call.Name, ["input"] = input });
                    }

                    break;
                case AiChatRole.Tool:
                    Append("user", new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = message.ToolCallId, ["content"] = message.Content });
                    break;
            }
        }

        var body = new JsonObject
        {
            ["model"] = connection.Model,
            ["max_tokens"] = Math.Min(request.MaxTokens ?? connection.MaxTokens, connection.MaxTokens),
            ["messages"] = messages,
            ["temperature"] = Math.Clamp(connection.Temperature, 0, 1),
        };
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            body["system"] = request.SystemPrompt;
        }

        if (request.Tools.Count > 0)
        {
            body["tools"] = new JsonArray(request.Tools.Select(t => (JsonNode)new JsonObject
            {
                ["name"] = t.Name,
                ["description"] = t.Description,
                ["input_schema"] = t.Parameters.DeepClone(),
            }).ToArray());
        }

        return body;
    }

    /// <summary>Parses a response body.</summary>
    /// <param name="root">The response JSON.</param>
    /// <returns>The response, or <see langword="null"/> when the shape is unexpected.</returns>
    public static AiChatResponse? Parse(JsonNode root)
    {
        if (root["content"] is not JsonArray blocks)
        {
            return null;
        }

        var text = new List<string>();
        var toolCalls = new List<AiToolCallRequest>();
        foreach (var block in blocks.OfType<JsonObject>())
        {
            var type = block["type"] is JsonValue typeValue && typeValue.GetValueKind() == JsonValueKind.String ? typeValue.GetValue<string>() : null;
            if (type == "text" && block["text"] is JsonValue t && t.GetValueKind() == JsonValueKind.String)
            {
                text.Add(t.GetValue<string>());
            }
            else if (type == "tool_use" && block["name"] is JsonValue name && name.GetValueKind() == JsonValueKind.String)
            {
                var id = block["id"] is JsonValue i && i.GetValueKind() == JsonValueKind.String ? i.GetValue<string>() : $"toolu_{toolCalls.Count}";
                toolCalls.Add(new AiToolCallRequest(id, name.GetValue<string>(), block["input"]?.ToJsonString() ?? "{}"));
            }
        }

        var usage = root["usage"] as JsonObject;
        return new AiChatResponse(
            string.Join("\n", text),
            toolCalls,
            root["stop_reason"] is JsonValue s && s.GetValueKind() == JsonValueKind.String ? s.GetValue<string>() : null,
            OpenAiWireFormat.ReadInt(usage?["input_tokens"]),
            OpenAiWireFormat.ReadInt(usage?["output_tokens"]));
    }
}
