namespace Servora.Application.Ai;

/// <summary>A provider-neutral chat completion response.</summary>
/// <param name="Content">The text output.</param>
/// <param name="ToolCalls">The requested tool calls.</param>
/// <param name="FinishReason">The provider's finish / stop reason.</param>
/// <param name="InputTokens">Input tokens reported by the provider.</param>
/// <param name="OutputTokens">Output tokens reported by the provider.</param>
public sealed record AiChatResponse(string Content, IReadOnlyList<AiToolCallRequest> ToolCalls, string? FinishReason, int InputTokens, int OutputTokens);
