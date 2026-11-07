using Servora.Domain.Ai;

namespace Servora.Application.Ai;

/// <summary>Provider configuration as shown in the admin UI (never contains the API key).</summary>
public sealed record AiProviderSummary
{
    /// <summary>Gets the identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the wire protocol.</summary>
    public AiProviderKind Kind { get; init; }

    /// <summary>Gets the base URL.</summary>
    public required string BaseUrl { get; init; }

    /// <summary>Gets the model.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>Gets the Azure deployment.</summary>
    public string? Deployment { get; init; }

    /// <summary>Gets the Azure API version.</summary>
    public string? ApiVersion { get; init; }

    /// <summary>Gets the maximum output tokens.</summary>
    public int MaxTokens { get; init; }

    /// <summary>Gets the temperature.</summary>
    public double Temperature { get; init; }

    /// <summary>Gets the timeout in seconds.</summary>
    public int TimeoutSeconds { get; init; }

    /// <summary>Gets a value indicating whether tool calling is supported.</summary>
    public bool SupportsTools { get; init; }

    /// <summary>Gets a value indicating whether the provider is enabled.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>Gets a value indicating whether the provider is the default.</summary>
    public bool IsDefault { get; init; }

    /// <summary>Gets a value indicating whether an API key is stored.</summary>
    public bool HasApiKey { get; init; }

    /// <summary>Gets the UTC time of the last test.</summary>
    public DateTimeOffset? LastTestedAt { get; init; }

    /// <summary>Gets the result of the last test.</summary>
    public bool? LastTestSucceeded { get; init; }

    /// <summary>Gets the message of the last test.</summary>
    public string? LastTestMessage { get; init; }
}
