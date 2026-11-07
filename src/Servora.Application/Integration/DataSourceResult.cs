namespace Servora.Application.Integration;

/// <summary>Bounded result of a data source execution.</summary>
/// <param name="StatusCode">The HTTP status code.</param>
/// <param name="ContentType">The response content type.</param>
/// <param name="Body">The (possibly truncated) response body as text.</param>
/// <param name="DurationMs">The duration in milliseconds.</param>
/// <param name="Truncated">Whether the body was truncated at the size limit.</param>
public sealed record DataSourceResult(int StatusCode, string? ContentType, string Body, long DurationMs, bool Truncated);
