using Servora.Domain.Common;

namespace Servora.Domain.Ai;

/// <summary>
/// Configuration of an AI model provider. The API key is never stored here; it lives encrypted in the secret store
/// under <see cref="SecretName"/>.
/// </summary>
public class AiProviderConfig : AuditableEntity
{
    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the wire protocol.</summary>
    public AiProviderKind Kind { get; set; }

    /// <summary>Gets or sets the base URL, for example <c>https://api.openai.com/v1</c> or <c>http://localhost:11434/v1</c>.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the model name (ignored by Azure OpenAI, which uses <see cref="Deployment"/>).</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Gets or sets the Azure OpenAI deployment name.</summary>
    public string? Deployment { get; set; }

    /// <summary>Gets or sets the Azure OpenAI API version.</summary>
    public string? ApiVersion { get; set; }

    /// <summary>Gets or sets the maximum number of output tokens per completion.</summary>
    public int MaxTokens { get; set; } = 1024;

    /// <summary>Gets or sets the sampling temperature (0–2).</summary>
    public double Temperature { get; set; } = 0.2;

    /// <summary>Gets or sets the request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Gets or sets a value indicating whether the model supports tool (function) calling.</summary>
    public bool SupportsTools { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the provider can be used.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether this is the default provider of the assistant.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Gets or sets the UTC time of the last connection test.</summary>
    public DateTimeOffset? LastTestedAt { get; set; }

    /// <summary>Gets or sets a value indicating whether the last connection test succeeded.</summary>
    public bool? LastTestSucceeded { get; set; }

    /// <summary>Gets or sets the user-safe message of the last connection test.</summary>
    public string? LastTestMessage { get; set; }

    /// <summary>Gets the secret store name of the provider API key.</summary>
    public string SecretName => $"ai.provider.{Id:N}.apikey";
}
