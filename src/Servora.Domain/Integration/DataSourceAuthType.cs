namespace Servora.Domain.Integration;

/// <summary>How requests to a REST data source are authenticated. Credentials always live in the secret store.</summary>
public enum DataSourceAuthType
{
    /// <summary>No authentication.</summary>
    None = 0,

    /// <summary>A static API key sent in a configurable request header.</summary>
    ApiKeyHeader = 1,

    /// <summary>A static bearer token (<c>Authorization: Bearer …</c>).</summary>
    Bearer = 2,

    /// <summary>HTTP basic authentication (only allowed over TLS).</summary>
    Basic = 3,

    /// <summary>OAuth 2.0 client credentials grant; tokens are cached in memory until shortly before expiry.</summary>
    OAuth2ClientCredentials = 4,
}
