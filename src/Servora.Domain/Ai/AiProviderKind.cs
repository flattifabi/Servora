namespace Servora.Domain.Ai;

/// <summary>Wire protocol of an AI provider.</summary>
public enum AiProviderKind
{
    /// <summary>OpenAI Chat Completions compatible endpoint (OpenAI, Ollama, LM Studio, vLLM, ...).</summary>
    OpenAiCompatible = 0,

    /// <summary>Anthropic Messages API.</summary>
    Anthropic = 1,

    /// <summary>Azure OpenAI deployment.</summary>
    AzureOpenAi = 2,
}
