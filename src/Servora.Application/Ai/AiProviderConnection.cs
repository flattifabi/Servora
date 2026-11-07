using Servora.Domain.Ai;

namespace Servora.Application.Ai;

/// <summary>
/// Resolved connection data of a provider including the decrypted API key. Backend use only: never bind it to UI
/// models, never log it (<see cref="ToString"/> is redacted).
/// </summary>
public sealed record AiProviderConnection
{
    /// <summary>Gets the provider identifier.</summary>
    public Guid ProviderId { get; init; }

    /// <summary>Gets the provider name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the wire protocol.</summary>
    public AiProviderKind Kind { get; init; }

    /// <summary>Gets the base URL.</summary>
    public required string BaseUrl { get; init; }

    /// <summary>Gets the model name.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>Gets the Azure deployment name.</summary>
    public string? Deployment { get; init; }

    /// <summary>Gets the Azure API version.</summary>
    public string? ApiVersion { get; init; }

    /// <summary>Gets the API key (may be empty for local servers).</summary>
    public string? ApiKey { get; init; }

    /// <summary>Gets the maximum output tokens.</summary>
    public int MaxTokens { get; init; } = 1024;

    /// <summary>Gets the temperature.</summary>
    public double Temperature { get; init; } = 0.2;

    /// <summary>Gets the timeout in seconds.</summary>
    public int TimeoutSeconds { get; init; } = 60;

    /// <summary>Gets a value indicating whether the model supports tool calling.</summary>
    public bool SupportsTools { get; init; } = true;

    /// <summary>Returns a redacted representation without the API key.</summary>
    /// <returns>The redacted string.</returns>
    public override string ToString() => $"AiProviderConnection {{ Name = {Name}, Kind = {Kind}, BaseUrl = {BaseUrl}, Model = {Model}, ApiKey = *** }}";
}
