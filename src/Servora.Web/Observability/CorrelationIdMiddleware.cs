using System.Text.RegularExpressions;

namespace Servora.Web.Observability;

/// <summary>
/// Assigns every HTTP request a correlation id: a well-formed incoming <c>X-Correlation-ID</c> header is reused,
/// otherwise a new id is generated. The id becomes <see cref="HttpContext.TraceIdentifier"/> (used by audit events),
/// is returned in the response header and is attached to every log entry of the request through a logging scope.
/// </summary>
/// <param name="next">The next middleware.</param>
/// <param name="logger">The logger used to open the scope.</param>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    /// <summary>The correlation id header name.</summary>
    public const string HeaderName = "X-Correlation-ID";

    /// <summary>Processes the request.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that completes when the pipeline is done.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        var correlationId = IsValid(incoming) ? incoming : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    /// <summary>Checks that an incoming correlation id is safe to reuse (1..64 characters of a restricted alphabet).</summary>
    /// <param name="value">The header value.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    public static bool IsValid(string? value) => !string.IsNullOrEmpty(value) && SafeId().IsMatch(value);

    [GeneratedRegex("^[A-Za-z0-9._:-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeId();
}
