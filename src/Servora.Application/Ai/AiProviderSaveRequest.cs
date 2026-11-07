using Servora.Domain.Ai;

namespace Servora.Application.Ai;

/// <summary>Input for creating or updating a provider configuration.</summary>
public sealed record AiProviderSaveRequest
{
    /// <summary>Gets the identifier of an existing provider, or <see langword="null"/> to create one.</summary>
    public Guid? Id { get; init; }

    /// <summary>Gets the name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the wire protocol.</summary>
    public AiProviderKind Kind { get; init; }

    /// <summary>Gets the base URL.</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>Gets the model.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>Gets the Azure deployment.</summary>
    public string? Deployment { get; init; }

    /// <summary>Gets the Azure API version.</summary>
    public string? ApiVersion { get; init; }

    /// <summary>Gets a new API key; <see langword="null"/> or empty keeps the stored key (write-only field).</summary>
    public string? ApiKey { get; init; }

    /// <summary>Gets a value indicating whether the stored API key should be removed.</summary>
    public bool ClearApiKey { get; init; }

    /// <summary>Gets the maximum output tokens.</summary>
    public int MaxTokens { get; init; } = 1024;

    /// <summary>Gets the temperature.</summary>
    public double Temperature { get; init; } = 0.2;

    /// <summary>Gets the timeout in seconds.</summary>
    public int TimeoutSeconds { get; init; } = 60;

    /// <summary>Gets a value indicating whether tool calling is supported.</summary>
    public bool SupportsTools { get; init; } = true;

    /// <summary>Gets a value indicating whether the provider is enabled.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>Gets a value indicating whether the provider is the default.</summary>
    public bool IsDefault { get; init; }

    /// <summary>Returns a redacted representation without the API key.</summary>
    /// <returns>The redacted string.</returns>
    public override string ToString() => $"AiProviderSaveRequest {{ Name = {Name}, Kind = {Kind}, BaseUrl = {BaseUrl}, ApiKey = *** }}";
}
