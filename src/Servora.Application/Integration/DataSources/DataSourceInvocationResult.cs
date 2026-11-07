namespace Servora.Application.Integration.DataSources;

/// <summary>Detailed, secret-free outcome of an HTTP data source call.</summary>
public sealed record DataSourceInvocationResult
{
    /// <summary>Gets the HTTP status code.</summary>
    public required int StatusCode { get; init; }

    /// <summary>Gets the response content type.</summary>
    public string? ContentType { get; init; }

    /// <summary>Gets the (possibly truncated) response body.</summary>
    public required string Body { get; init; }

    /// <summary>Gets the part selected by the endpoint's response path (JSON), if configured and found.</summary>
    public string? MappedBody { get; init; }

    /// <summary>Gets a value indicating whether the body was truncated at the size limit.</summary>
    public bool Truncated { get; init; }

    /// <summary>Gets the number of bytes read.</summary>
    public long ResponseBytes { get; init; }

    /// <summary>Gets the total duration in milliseconds.</summary>
    public long DurationMs { get; init; }

    /// <summary>Gets the number of attempts.</summary>
    public int Attempts { get; init; } = 1;

    /// <summary>Gets the final target host (after redirects).</summary>
    public string? Host { get; init; }

    /// <summary>Gets the HTTP method.</summary>
    public string Method { get; init; } = "GET";

    /// <summary>Gets a whitelisted subset of response headers (content metadata and rate limit headers).</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
}
