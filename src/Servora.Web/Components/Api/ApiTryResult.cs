namespace Servora.Web.Components.Api;

/// <summary>Result of a "try it" request executed in the browser with the user's cookie.</summary>
/// <param name="Status">The HTTP status (0 when the request failed).</param>
/// <param name="StatusText">The status text.</param>
/// <param name="DurationMs">The round-trip time.</param>
/// <param name="Headers">The response headers (one per line).</param>
/// <param name="Body">The (possibly truncated) response body.</param>
/// <param name="Error">A network error message.</param>
public sealed record ApiTryResult(int Status, string? StatusText, long DurationMs, string? Headers, string? Body, string? Error);
