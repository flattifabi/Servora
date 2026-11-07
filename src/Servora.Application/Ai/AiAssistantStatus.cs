namespace Servora.Application.Ai;

/// <summary>Readiness of the assistant for the current user.</summary>
/// <param name="HasProvider">Whether an enabled provider is configured.</param>
/// <param name="ProviderName">The provider name.</param>
/// <param name="Model">The model (or deployment).</param>
/// <param name="SupportsTools">Whether the model supports tool calling.</param>
/// <param name="CanConfigure">Whether the user may configure providers.</param>
/// <param name="Agents">The enabled agents.</param>
public sealed record AiAssistantStatus(bool HasProvider, string? ProviderName, string? Model, bool SupportsTools, bool CanConfigure, IReadOnlyList<AiAgentInfo> Agents);
