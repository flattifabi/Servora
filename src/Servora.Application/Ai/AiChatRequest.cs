namespace Servora.Application.Ai;

/// <summary>A provider-neutral chat completion request.</summary>
public sealed record AiChatRequest
{
    /// <summary>Gets the system prompt.</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>Gets the conversation messages.</summary>
    public IReadOnlyList<AiChatMessage> Messages { get; init; } = [];

    /// <summary>Gets the tools offered to the model (empty = no tool calling).</summary>
    public IReadOnlyList<AiToolDefinition> Tools { get; init; } = [];

    /// <summary>Gets an override of the maximum output tokens (bounded by the provider setting).</summary>
    public int? MaxTokens { get; init; }
}
