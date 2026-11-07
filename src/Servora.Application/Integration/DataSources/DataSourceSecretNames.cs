namespace Servora.Application.Integration.DataSources;

/// <summary>Names of the secret store entries holding data source credentials, and related cache keys.</summary>
public static class DataSourceSecretNames
{
    /// <summary>Gets the secret name of the API key.</summary>
    /// <param name="dataSourceId">The data source.</param>
    /// <returns>The secret name.</returns>
    public static string ApiKey(Guid dataSourceId) => $"datasource.{dataSourceId:N}.api_key";

    /// <summary>Gets the secret name of the bearer token.</summary>
    /// <param name="dataSourceId">The data source.</param>
    /// <returns>The secret name.</returns>
    public static string BearerToken(Guid dataSourceId) => $"datasource.{dataSourceId:N}.bearer_token";

    /// <summary>Gets the secret name of the basic authentication password.</summary>
    /// <param name="dataSourceId">The data source.</param>
    /// <returns>The secret name.</returns>
    public static string BasicPassword(Guid dataSourceId) => $"datasource.{dataSourceId:N}.basic_password";

    /// <summary>Gets the secret name of the OAuth 2.0 client secret.</summary>
    /// <param name="dataSourceId">The data source.</param>
    /// <returns>The secret name.</returns>
    public static string OAuthClientSecret(Guid dataSourceId) => $"datasource.{dataSourceId:N}.oauth_client_secret";

    /// <summary>Gets the memory cache key of a cached OAuth access token.</summary>
    /// <param name="dataSourceId">The data source.</param>
    /// <returns>The cache key.</returns>
    public static string OAuthTokenCacheKey(Guid dataSourceId) => $"datasource.{dataSourceId:N}.oauth_token";

    /// <summary>Gets all secret names of a data source.</summary>
    /// <param name="dataSourceId">The data source.</param>
    /// <returns>The secret names.</returns>
    public static IReadOnlyList<string> All(Guid dataSourceId) =>
        [ApiKey(dataSourceId), BearerToken(dataSourceId), BasicPassword(dataSourceId), OAuthClientSecret(dataSourceId)];
}
