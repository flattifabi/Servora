using Servora.Domain.Common;

namespace Servora.Domain.Integration;

/// <summary>
/// A configured outbound REST data source. Only its approved <see cref="DataSourceEndpoint"/>s can be executed; arbitrary
/// URLs are never requested. Credentials are stored in the secret store under <c>datasource.&lt;id&gt;.*</c>, never here.
/// </summary>
public class DataSource : AuditableEntity
{
    /// <summary>Gets or sets the unique display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the absolute base URL (HTTPS unless <see cref="AllowHttp"/> is set).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the authentication type.</summary>
    public DataSourceAuthType AuthType { get; set; } = DataSourceAuthType.None;

    /// <summary>Gets or sets the header name carrying the API key for <see cref="DataSourceAuthType.ApiKeyHeader"/>.</summary>
    public string? ApiKeyHeaderName { get; set; }

    /// <summary>Gets or sets the user name for <see cref="DataSourceAuthType.Basic"/>.</summary>
    public string? BasicUserName { get; set; }

    /// <summary>Gets or sets the OAuth 2.0 token endpoint URL.</summary>
    public string? OAuthTokenUrl { get; set; }

    /// <summary>Gets or sets the OAuth 2.0 client identifier.</summary>
    public string? OAuthClientId { get; set; }

    /// <summary>Gets or sets the OAuth 2.0 scope.</summary>
    public string? OAuthScope { get; set; }

    /// <summary>Gets or sets the JSON object of default request headers (no credentials).</summary>
    public string? DefaultHeadersJson { get; set; }

    /// <summary>Gets or sets the request timeout in seconds (1–120).</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Gets or sets the number of retries for transient failures of idempotent requests (0–5).</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>Gets or sets the maximum response body size in bytes; larger bodies are truncated.</summary>
    public int MaxResponseBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets the maximum number of requests per minute (0 = unlimited).</summary>
    public int RateLimitPerMinute { get; set; } = 60;

    /// <summary>Gets or sets a value indicating whether plain HTTP is allowed (discouraged; shows a warning).</summary>
    public bool AllowHttp { get; set; }

    /// <summary>Gets or sets a value indicating whether the data source can be executed.</summary>
    public bool IsEnabled { get; set; } = true;
}
