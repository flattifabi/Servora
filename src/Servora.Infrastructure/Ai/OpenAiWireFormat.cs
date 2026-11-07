using System.Text.Json;
using System.Text.Json.Nodes;
using Servora.Application.Ai;

namespace Servora.Infrastructure.Ai;

/// <summary>Request / response mapping of the OpenAI Chat Completions format (also used by Azure OpenAI, Ollama, LM Studio).</summary>
public static class OpenAiWireFormat
{
    /// <summary>Builds the request body.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="request">The request.</param>
    /// <param name="includeModel">Whether to send the model name (not for Azure deployments).</param>
    /// <param name="useMaxCompletionTokens">Whether to use <c>max_completion_tokens</c> (api.openai.com) instead of <c>max_tokens</c>.</param>
    /// <returns>The body.</returns>
    public static JsonObject BuildBody(AiProviderConnection connection, AiChatRequest request, bool includeModel, bool useMaxCompletionTokens)
    {
        var messages = new JsonArray();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt });
        }

        foreach (var message in request.Messages)
        {
            switch (message.Role)
            {
                case AiChatRole.User:
                    messages.Add(new JsonObject { ["role"] = "user", ["content"] = message.Content });
                    break;
                case AiChatRole.Assistant:
                    var assistant = new JsonObject { ["role"] = "assistant", ["content"] = string.IsNullOrEmpty(message.Content) && message.ToolCalls.Count > 0 ? null : message.Content };
                    if (message.ToolCalls.Count > 0)
                    {
                        assistant["tool_calls"] = new JsonArray(message.ToolCalls.Select(c => (JsonNode)new JsonObject
                        {
                            ["id"] = c.Id,
                            ["type"] = "function",
                            ["function"] = new JsonObject { ["name"] = c.Name, ["arguments"] = string.IsNullOrWhiteSpace(c.ArgumentsJson) ? "{}" : c.ArgumentsJson },
                        }).ToArray());
                    }

                    messages.Add(assistant);
                    break;
                case AiChatRole.Tool:
                    messages.Add(new JsonObject { ["role"] = "tool", ["tool_call_id"] = message.ToolCallId, ["content"] = message.Content });
                    break;
            }
        }

        var body = new JsonObject { ["messages"] = messages };
        if (includeModel)
        {
            body["model"] = connection.Model;
        }

        body[useMaxCompletionTokens ? "max_completion_tokens" : "max_tokens"] = Math.Min(request.MaxTokens ?? connection.MaxTokens, connection.MaxTokens);

        // 1.0 is the API default; omitting it keeps reasoning models (which reject custom temperatures) working.
        if (Math.Abs(connection.Temperature - 1.0) > 0.0001)
        {
            body["temperature"] = connection.Temperature;
        }

        if (request.Tools.Count > 0)
        {
            body["tools"] = new JsonArray(request.Tools.Select(t => (JsonNode)new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = t.Name, ["description"] = t.Description, ["parameters"] = t.Parameters.DeepClone() },
            }).ToArray());
            body["tool_choice"] = "auto";
        }

        return body;
    }

    /// <summary>Parses a response body.</summary>
    /// <param name="root">The response JSON.</param>
    /// <returns>The response, or <see langword="null"/> when the shape is unexpected.</returns>
    public static AiChatResponse? Parse(JsonNode root)
    {
        if (root["choices"] is not JsonArray { Count: > 0 } choices || choices[0] is not JsonObject choice || choice["message"] is not JsonObject message)
        {
            return null;
        }

        var content = message["content"] is JsonValue c && c.GetValueKind() == JsonValueKind.String ? c.GetValue<string>() : string.Empty;
        var toolCalls = new List<AiToolCallRequest>();
        if (message["tool_calls"] is JsonArray calls)
        {
            foreach (var call in calls.OfType<JsonObject>())
            {
                if (call["function"] is not JsonObject function || function["name"] is not JsonValue name || name.GetValueKind() != JsonValueKind.String)
                {
                    continue;
                }

                var arguments = function["arguments"] switch
                {
                    JsonValue a when a.GetValueKind() == JsonValueKind.String => a.GetValue<string>(),
                    JsonObject o => o.ToJsonString(),
                    _ => "{}",
                };
                var id = call["id"] is JsonValue i && i.GetValueKind() == JsonValueKind.String ? i.GetValue<string>() : $"call_{toolCalls.Count}";
                toolCalls.Add(new AiToolCallRequest(id, name.GetValue<string>(), arguments));
            }
        }

        var usage = root["usage"] as JsonObject;
        return new AiChatResponse(
            content,
            toolCalls,
            choice["finish_reason"] is JsonValue f && f.GetValueKind() == JsonValueKind.String ? f.GetValue<string>() : null,
            ReadInt(usage?["prompt_tokens"]),
            ReadInt(usage?["completion_tokens"]));
    }

    /// <summary>Reads an integer token count.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The value or 0.</returns>
    internal static int ReadInt(JsonNode? node) =>
        node is JsonValue v && v.GetValueKind() == JsonValueKind.Number && int.TryParse(v.ToJsonString(), out var value) ? value : 0;
}
