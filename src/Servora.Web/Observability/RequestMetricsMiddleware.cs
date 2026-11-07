using System.Diagnostics;
using Servora.Application.Observability;

namespace Servora.Web.Observability;

/// <summary>
/// Measures every HTTP request (count, latency, status) into <see cref="IRequestMetrics"/>. Requests are keyed by method
/// and route template (never raw paths with identifiers); static assets are grouped, Blazor circuit connections and
/// WebSockets are excluded because they are long-lived.
/// </summary>
/// <param name="next">The next middleware.</param>
/// <param name="metrics">The metrics collector.</param>
public sealed class RequestMetricsMiddleware(RequestDelegate next, IRequestMetrics metrics)
{
    /// <summary>Processes the request.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that completes when the pipeline is done.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (context.WebSockets.IsWebSocketRequest || path.StartsWithSegments("/_blazor"))
        {
            await next(context);
            return;
        }

        var start = Stopwatch.GetTimestamp();
        var statusCode = StatusCodes.Status500InternalServerError;
        try
        {
            await next(context);
            statusCode = context.Response.StatusCode;
        }
        finally
        {
            metrics.Record(RouteKey(context), statusCode, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
    }

    /// <summary>Builds the low-cardinality metrics key of a request.</summary>
    /// <param name="context">The HTTP context (after the endpoint ran).</param>
    /// <returns>The key, for example <c>GET /admin/logs</c> or <c>static</c>.</returns>
    public static string RouteKey(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        if (path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/_content", StringComparison.OrdinalIgnoreCase) || Path.HasExtension(path))
        {
            return "static assets";
        }

        var pattern = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        if (string.IsNullOrEmpty(pattern))
        {
            return context.Response.StatusCode == StatusCodes.Status404NotFound ? "(not found)" : "(unmatched)";
        }

        return $"{context.Request.Method} /{pattern.TrimStart('/')}";
    }
}
