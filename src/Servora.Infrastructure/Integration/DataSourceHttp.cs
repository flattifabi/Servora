using System.Net;
using System.Net.Http;

namespace Servora.Infrastructure.Integration;

/// <summary>Configuration of the named <see cref="HttpClient"/> used for data source calls.</summary>
public static class DataSourceHttp
{
    /// <summary>The name of the HTTP client registered with <see cref="IHttpClientFactory"/>.</summary>
    public const string ClientName = "Servora.DataSources";

    /// <summary>Creates the primary handler: SSRF-checked connect callback, no automatic redirects, no proxy, no cookies.</summary>
    /// <param name="guard">The SSRF guard.</param>
    /// <returns>The handler.</returns>
    public static SocketsHttpHandler CreateHandler(SsrfGuard guard) => new()
    {
        // Redirects are followed manually by the invoker so every hop is re-validated (max 3).
        AllowAutoRedirect = false,

        // A proxy would make the connect-time IP check meaningless.
        UseProxy = false,
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.All,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        MaxResponseHeadersLength = 64,
        MaxConnectionsPerServer = 16,
        ConnectCallback = guard.ConnectAsync,

        // Certificate validation stays at the platform default (always on); no custom validation callback.
    };
}
