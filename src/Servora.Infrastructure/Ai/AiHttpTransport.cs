using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Servora.Application.Ai;
using Servora.Application.Common;
using Servora.Application.Mcp;

namespace Servora.Infrastructure.Ai;

/// <summary>
/// Shared HTTP plumbing of the AI provider clients: per-request timeout, bounded responses, no redirects, error
/// messages sanitized so that API keys can never leak into results or logs.
/// </summary>
/// <param name="httpClientFactory">The HTTP client factory.</param>
/// <param name="logger">The logger.</param>
public sealed class AiHttpTransport(IHttpClientFactory httpClientFactory, ILogger<AiHttpTransport> logger)
{
    /// <summary>The name of the configured HTTP client.</summary>
    public const string ClientName = "servora-ai";

    /// <summary>The maximum response size in bytes.</summary>
    public const int MaxResponseBytes = 4 * 1024 * 1024;

    /// <summary>Posts a JSON body and parses the JSON response.</summary>
    /// <param name="connection">The connection (timeout, key for redaction).</param>
    /// <param name="url">The endpoint URL.</param>
    /// <param name="body">The request body.</param>
    /// <param name="configureHeaders">Adds authentication and version headers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The parsed response or a user-safe failure.</returns>
    public async Task<Result<JsonNode>> PostJsonAsync(
        AiProviderConnection connection,
        Uri url,
        JsonObject body,
        Action<HttpRequestHeaders> configureHeaders,
        CancellationToken cancellationToken)
    {
        var timeoutSeconds = Math.Clamp(connection.TimeoutSeconds, 5, 600);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        configureHeaders(request.Headers);

        try
        {
            var client = httpClientFactory.CreateClient(ClientName);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token);
            var text = await response.Content.ReadAsStringAsync(timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                var error = Redact(ExtractError(text), connection.ApiKey);
                logger.LogWarning("AI provider {Provider} returned HTTP {Status}: {Error}", connection.Name, (int)response.StatusCode, error);
                return Result<JsonNode>.Failure($"The provider returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}): {error}");
            }

            if (!McpJson.TryParse(text, out var node) || node is null)
            {
                return Result<JsonNode>.Failure("The provider returned a response that is not valid JSON.");
            }

            return Result<JsonNode>.Success(node);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<JsonNode>.Failure($"The provider did not answer within {timeoutSeconds} seconds.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "AI provider {Provider} at {Host} is unreachable.", connection.Name, url.Host);
            return Result<JsonNode>.Failure($"Could not reach the provider at {url.Host}: {Redact(ex.Message, connection.ApiKey)}");
        }
    }

    /// <summary>Extracts a short error message from a provider error body.</summary>
    /// <param name="body">The body.</param>
    /// <returns>The message (max 300 characters).</returns>
    internal static string ExtractError(string body)
    {
        if (McpJson.TryParse(body, out var node) && node is JsonObject root)
        {
            var message = root["error"] switch
            {
                JsonObject error when error["message"] is JsonValue m && m.GetValueKind() == JsonValueKind.String => m.GetValue<string>(),
                JsonValue error when error.GetValueKind() == JsonValueKind.String => error.GetValue<string>(),
                _ => root["message"] is JsonValue rm && rm.GetValueKind() == JsonValueKind.String ? rm.GetValue<string>() : null,
            };
            if (!string.IsNullOrWhiteSpace(message))
            {
                return McpJson.Truncate(message.Trim(), 300);
            }
        }

        return string.IsNullOrWhiteSpace(body) ? "no details" : McpJson.Truncate(body.Trim(), 300);
    }

    /// <summary>Removes the API key from a message.</summary>
    /// <param name="message">The message.</param>
    /// <param name="apiKey">The API key.</param>
    /// <returns>The redacted message.</returns>
    internal static string Redact(string message, string? apiKey) =>
        string.IsNullOrEmpty(apiKey) || apiKey.Length < 4 ? message : message.Replace(apiKey, "***", StringComparison.Ordinal);
}
