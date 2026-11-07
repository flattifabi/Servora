using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Servora.Application.Automation.Templates;
using Servora.Application.Common;
using Servora.Application.Integration.DataSources;
using Servora.Application.Security;
using Servora.Domain.Integration;

namespace Servora.Infrastructure.Integration;

/// <summary>
/// <see cref="IDataSourceInvoker"/> over the named, SSRF-protected <see cref="HttpClient"/>: builds and validates the
/// request, applies authentication from the secret store (never logged), follows at most
/// <see cref="MaxRedirects"/> redirects with re-validation (credentials are dropped when the host changes, HTTPS is
/// never downgraded), enforces timeouts and the response size limit, and retries transient failures of idempotent
/// requests with exponential backoff.
/// </summary>
/// <param name="httpClientFactory">The HTTP client factory.</param>
/// <param name="secrets">The secret store.</param>
/// <param name="cache">The memory cache (OAuth tokens).</param>
/// <param name="guard">The SSRF guard.</param>
/// <param name="logger">The logger.</param>
public sealed class HttpDataSourceInvoker(
    IHttpClientFactory httpClientFactory,
    ISecretStore secrets,
    IMemoryCache cache,
    SsrfGuard guard,
    ILogger<HttpDataSourceInvoker> logger) : IDataSourceInvoker
{
    /// <summary>The maximum number of redirects that are followed.</summary>
    public const int MaxRedirects = 3;

    /// <summary>The hard upper limit of the response size.</summary>
    public const int AbsoluteMaxResponseBytes = 10 * 1024 * 1024;

    private static readonly string[] s_exposedHeaders = ["Content-Type", "Content-Length", "Date", "ETag", "Last-Modified", "Retry-After", "X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset"];

    /// <summary>Gets or sets the base backoff delay between retries (exposed for tests).</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <inheritdoc />
    public async Task<Result<DataSourceInvocationResult>> InvokeAsync(DataSource source, DataSourceEndpoint endpoint, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {
        var planResult = DataSourceRequestBuilder.Build(source, endpoint, parameters);
        if (!planResult.Succeeded)
        {
            return Result<DataSourceInvocationResult>.From(planResult);
        }

        var plan = planResult.Value!;
        if (await guard.ValidateUriAsync(plan.Uri, source.AllowHttp, cancellationToken) is { } blocked)
        {
            return Result<DataSourceInvocationResult>.Failure(blocked);
        }

        var auth = await GetAuthorizationAsync(source, plan.Uri, cancellationToken);
        if (!auth.Succeeded)
        {
            return Result<DataSourceInvocationResult>.From(auth);
        }

        var headers = DataSourceHeaderRules.Parse(source.DefaultHeadersJson);
        var timeout = TimeSpan.FromSeconds(Math.Clamp(source.TimeoutSeconds, 1, 120));
        var maxBytes = Math.Clamp(source.MaxResponseBytes, 1024, AbsoluteMaxResponseBytes);
        var idempotent = plan.Method is "GET" or "PUT" or "DELETE";
        var maxAttempts = idempotent ? 1 + Math.Clamp(source.MaxRetries, 0, 5) : 1;
        var client = httpClientFactory.CreateClient(DataSourceHttp.ClientName);
        var watch = Stopwatch.StartNew();
        var lastError = "The request failed.";

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (attempt > 1)
            {
                await Task.Delay(RetryBaseDelay * Math.Pow(2, attempt - 2), cancellationToken);
            }

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            try
            {
                var (response, host) = await SendWithRedirectsAsync(client, plan, auth.Value, headers, source.AllowHttp, timeoutSource.Token);
                using (response)
                {
                    var status = (int)response.StatusCode;
                    if (attempt < maxAttempts && (status is 408 or 429 || status >= 500))
                    {
                        lastError = $"HTTP {status}";
                        continue;
                    }

                    var (body, bytes, truncated) = await ReadBodyAsync(response, maxBytes, timeoutSource.Token);
                    var contentType = response.Content.Headers.ContentType?.ToString();
                    return Result<DataSourceInvocationResult>.Success(new DataSourceInvocationResult
                    {
                        StatusCode = status,
                        ContentType = contentType,
                        Body = body,
                        MappedBody = truncated ? null : MapResponse(body, endpoint.ResponsePath),
                        Truncated = truncated,
                        ResponseBytes = bytes,
                        DurationMs = watch.ElapsedMilliseconds,
                        Attempts = attempt,
                        Host = host,
                        Method = plan.Method,
                        Headers = ExposedHeaders(response),
                    });
                }
            }
            catch (DataSourceBlockedException ex)
            {
                return Result<DataSourceInvocationResult>.Failure(ex.Message);
            }
            catch (HttpRequestException ex) when (FindBlocked(ex) is { } blockedException)
            {
                return Result<DataSourceInvocationResult>.Failure(blockedException.Message);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning("Data source {DataSource} endpoint {Endpoint}: request failed ({Error}).", source.Name, endpoint.Key, ex.HttpRequestError);
                lastError = ex.HttpRequestError switch
                {
                    HttpRequestError.NameResolutionError => "The host name could not be resolved.",
                    HttpRequestError.SecureConnectionError => "The TLS connection failed (certificate validation is always enforced).",
                    HttpRequestError.ConnectionError => "The connection to the server failed.",
                    _ => "The request failed.",
                };
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastError = $"The request timed out after {timeout.TotalSeconds:0} seconds.";
            }
        }

        return Result<DataSourceInvocationResult>.Failure(lastError);
    }

    /// <summary>Selects the response part addressed by a JSON path.</summary>
    /// <param name="body">The body.</param>
    /// <param name="path">The path (empty = no mapping).</param>
    /// <returns>The selected JSON or <see langword="null"/>.</returns>
    public static string? MapResponse(string body, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var node = JsonNode.Parse(body, documentOptions: new JsonDocumentOptions { MaxDepth = 64 });
            var selected = FlowTemplate.Lookup(node, path.Trim());
            return selected?.ToJsonString();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DataSourceBlockedException? FindBlocked(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DataSourceBlockedException blocked)
            {
                return blocked;
            }
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string> ExposedHeaders(HttpResponseMessage response)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in s_exposedHeaders)
        {
            if (response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values))
            {
                result[name] = string.Join(", ", values);
            }
        }

        return result;
    }

    private static async Task<(string Body, long Bytes, bool Truncated)> ReadBodyAsync(HttpResponseMessage response, int maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[Math.Min(maxBytes + 1, 81_920)];
        using var memory = new MemoryStream();
        var truncated = false;
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            var remaining = maxBytes - (int)memory.Length;
            if (read > remaining)
            {
                memory.Write(buffer, 0, remaining);
                truncated = true;
                break;
            }

            memory.Write(buffer, 0, read);
        }

        var encoding = Encoding.UTF8;
        var charset = response.Content.Headers.ContentType?.CharSet?.Trim('"');
        if (!string.IsNullOrEmpty(charset))
        {
            try
            {
                encoding = Encoding.GetEncoding(charset);
            }
            catch (ArgumentException)
            {
                encoding = Encoding.UTF8;
            }
        }

        return (encoding.GetString(memory.GetBuffer(), 0, (int)memory.Length), memory.Length, truncated);
    }

    private async Task<(HttpResponseMessage Response, string Host)> SendWithRedirectsAsync(
        HttpClient client,
        DataSourceRequestPlan plan,
        DataSourceAuthHeader? auth,
        IReadOnlyDictionary<string, string> headers,
        bool allowHttp,
        CancellationToken cancellationToken)
    {
        var current = plan.Uri;
        var method = plan.Method;
        var body = plan.Body;
        var sendCredentials = true;
        for (var hop = 0; ; hop++)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), current);
            request.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain;q=0.9, */*;q=0.5");
            request.Headers.TryAddWithoutValidation("User-Agent", "Servora-DataSource/1.0");
            foreach (var header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (sendCredentials && auth is not null)
            {
                request.Headers.TryAddWithoutValidation(auth.Name, auth.Value);
            }

            if (body is not null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var status = (int)response.StatusCode;
            if (status is not (301 or 302 or 303 or 307 or 308) || response.Headers.Location is null)
            {
                return (response, current.Host);
            }

            var location = response.Headers.Location;
            response.Dispose();
            if (hop >= MaxRedirects)
            {
                throw new DataSourceBlockedException($"Too many redirects (maximum {MaxRedirects}).");
            }

            var next = location.IsAbsoluteUri ? location : new Uri(current, location);
            if (current.Scheme == Uri.UriSchemeHttps && next.Scheme != Uri.UriSchemeHttps && !allowHttp)
            {
                throw new DataSourceBlockedException("A redirect from HTTPS to an insecure URL was blocked.");
            }

            if (await guard.ValidateUriAsync(next, allowHttp, cancellationToken) is { } blocked)
            {
                throw new DataSourceBlockedException($"Redirect blocked. {blocked}");
            }

            if (!string.Equals(next.IdnHost, current.IdnHost, StringComparison.OrdinalIgnoreCase) || next.Port != current.Port || next.Scheme != current.Scheme)
            {
                // Never forward credentials to another origin.
                sendCredentials = false;
            }

            if (status == 303 || (status is 301 or 302 && method == "POST"))
            {
                method = "GET";
                body = null;
            }

            current = next;
        }
    }

    private async Task<Result<DataSourceAuthHeader?>> GetAuthorizationAsync(DataSource source, Uri target, CancellationToken cancellationToken)
    {
        switch (source.AuthType)
        {
            case DataSourceAuthType.None:
                return Result<DataSourceAuthHeader?>.Success(null);
            case DataSourceAuthType.ApiKeyHeader:
            {
                var headerName = string.IsNullOrWhiteSpace(source.ApiKeyHeaderName) ? "X-API-Key" : source.ApiKeyHeaderName.Trim();
                var key = await secrets.GetAsync(DataSourceSecretNames.ApiKey(source.Id), cancellationToken);
                return string.IsNullOrEmpty(key)
                    ? Result<DataSourceAuthHeader?>.Invalid("The API key of this data source is not configured.")
                    : Result<DataSourceAuthHeader?>.Success(new DataSourceAuthHeader(headerName, key));
            }

            case DataSourceAuthType.Bearer:
            {
                var token = await secrets.GetAsync(DataSourceSecretNames.BearerToken(source.Id), cancellationToken);
                return string.IsNullOrEmpty(token)
                    ? Result<DataSourceAuthHeader?>.Invalid("The bearer token of this data source is not configured.")
                    : Result<DataSourceAuthHeader?>.Success(new DataSourceAuthHeader("Authorization", "Bearer " + token));
            }

            case DataSourceAuthType.Basic:
            {
                if (target.Scheme != Uri.UriSchemeHttps)
                {
                    return Result<DataSourceAuthHeader?>.Invalid("Basic authentication is only allowed over HTTPS.");
                }

                var password = await secrets.GetAsync(DataSourceSecretNames.BasicPassword(source.Id), cancellationToken);
                if (string.IsNullOrEmpty(source.BasicUserName) || password is null)
                {
                    return Result<DataSourceAuthHeader?>.Invalid("User name and password of this data source are not configured.");
                }

                var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{source.BasicUserName}:{password}"));
                return Result<DataSourceAuthHeader?>.Success(new DataSourceAuthHeader("Authorization", "Basic " + encoded));
            }

            case DataSourceAuthType.OAuth2ClientCredentials:
            {
                var token = await GetOAuthTokenAsync(source, cancellationToken);
                return token.Succeeded
                    ? Result<DataSourceAuthHeader?>.Success(new DataSourceAuthHeader("Authorization", "Bearer " + token.Value))
                    : Result<DataSourceAuthHeader?>.From(token);
            }

            default:
                return Result<DataSourceAuthHeader?>.Invalid("Unsupported authentication type.");
        }
    }

    private async Task<Result<string>> GetOAuthTokenAsync(DataSource source, CancellationToken cancellationToken)
    {
        var cacheKey = DataSourceSecretNames.OAuthTokenCacheKey(source.Id);
        if (cache.TryGetValue(cacheKey, out string? cachedToken) && !string.IsNullOrEmpty(cachedToken))
        {
            return Result<string>.Success(cachedToken);
        }

        if (!Uri.TryCreate(source.OAuthTokenUrl, UriKind.Absolute, out var tokenUri) || tokenUri.Scheme != Uri.UriSchemeHttps)
        {
            return Result<string>.Invalid("The OAuth token URL must be an absolute HTTPS URL.");
        }

        if (await guard.ValidateUriAsync(tokenUri, allowHttp: false, cancellationToken) is { } blocked)
        {
            return Result<string>.Failure(blocked);
        }

        var clientSecret = await secrets.GetAsync(DataSourceSecretNames.OAuthClientSecret(source.Id), cancellationToken);
        if (string.IsNullOrEmpty(source.OAuthClientId) || string.IsNullOrEmpty(clientSecret))
        {
            return Result<string>.Invalid("Client id and client secret of this data source are not configured.");
        }

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = source.OAuthClientId,
            ["client_secret"] = clientSecret,
        };
        if (!string.IsNullOrWhiteSpace(source.OAuthScope))
        {
            form["scope"] = source.OAuthScope;
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(source.TimeoutSeconds, 1, 120)));
        try
        {
            var client = httpClientFactory.CreateClient(DataSourceHttp.ClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, tokenUri) { Content = new FormUrlEncodedContent(form) };
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token);
            if (!response.IsSuccessStatusCode)
            {
                return Result<string>.Failure($"The OAuth token request failed with HTTP {(int)response.StatusCode}.");
            }

            var (body, _, truncated) = await ReadBodyAsync(response, 64 * 1024, timeoutSource.Token);
            if (truncated)
            {
                return Result<string>.Failure("The OAuth token response is too large.");
            }

            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("access_token", out var accessToken) || accessToken.GetString() is not { Length: > 0 } token)
            {
                return Result<string>.Failure("The OAuth token response contains no access token.");
            }

            var lifetime = document.RootElement.TryGetProperty("expires_in", out var expires) && expires.TryGetInt32(out var seconds) ? seconds : 300;
            cache.Set(cacheKey, token, TimeSpan.FromSeconds(Math.Clamp(lifetime - 60, 30, 3600)));
            return Result<string>.Success(token);
        }
        catch (HttpRequestException ex) when (FindBlocked(ex) is { } blockedException)
        {
            return Result<string>.Failure(blockedException.Message);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return Result<string>.Failure("The OAuth token could not be obtained.");
        }
    }
}
