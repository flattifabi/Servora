using Servora.Domain.Integration;

namespace Servora.Application.Integration.DataSources;

/// <summary>Editable configuration of a data source (credentials are write-only and never part of this model).</summary>
public sealed class DataSourceModel
{
    /// <summary>Gets or sets the identifier (empty for new sources).</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the base URL.</summary>
    public string BaseUrl { get; set; } = "https://";

    /// <summary>Gets or sets the authentication type.</summary>
    public DataSourceAuthType AuthType { get; set; }

    /// <summary>Gets or sets the API key header name.</summary>
    public string? ApiKeyHeaderName { get; set; } = "X-API-Key";

    /// <summary>Gets or sets the basic authentication user name.</summary>
    public string? BasicUserName { get; set; }

    /// <summary>Gets or sets the OAuth token URL.</summary>
    public string? OAuthTokenUrl { get; set; }

    /// <summary>Gets or sets the OAuth client id.</summary>
    public string? OAuthClientId { get; set; }

    /// <summary>Gets or sets the OAuth scope.</summary>
    public string? OAuthScope { get; set; }

    /// <summary>Gets or sets the default headers.</summary>
    public Dictionary<string, string> DefaultHeaders { get; set; } = [];

    /// <summary>Gets or sets the timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Gets or sets the retry count.</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>Gets or sets the maximum response size in bytes.</summary>
    public int MaxResponseBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets the rate limit per minute.</summary>
    public int RateLimitPerMinute { get; set; } = 60;

    /// <summary>Gets or sets a value indicating whether plain HTTP is allowed.</summary>
    public bool AllowHttp { get; set; }

    /// <summary>Gets or sets a value indicating whether the source is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the credential of the current auth type is stored (read only).</summary>
    public bool CredentialConfigured { get; set; }

    /// <summary>Gets or sets the concurrency token.</summary>
    public long ConcurrencyVersion { get; set; }

    /// <summary>Gets or sets the last modification (read only).</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Gets or sets the last modifier (read only).</summary>
    public string? UpdatedBy { get; set; }
}
