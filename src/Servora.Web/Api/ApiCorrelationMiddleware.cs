using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace Servora.Web.Api;

/// <summary>
/// For API paths: adopts a valid incoming <c>X-Correlation-Id</c> (or uses the trace identifier), echoes it in the
/// response, makes it the request's trace identifier (so audit events carry it) and converts unhandled exceptions into
/// RFC 7807 problem responses without internal details.
/// </summary>
/// <param name="next">The next middleware.</param>
/// <param name="logger">The logger.</param>
public sealed partial class ApiCorrelationMiddleware(RequestDelegate next, ILogger<ApiCorrelationMiddleware> logger)
{
    /// <summary>The correlation header.</summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>Processes the request.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that completes when the pipeline is done.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!ApiCredentialReader.IsApiPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        var incoming = context.Request.Headers[HeaderName].ToString();
        var correlationId = ValidId().IsMatch(incoming) ? incoming : context.TraceIdentifier;
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted && !context.RequestAborted.IsCancellationRequested)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path} (correlation id {CorrelationId}).", context.Request.Method, context.Request.Path, correlationId);
            context.Response.Clear();
            context.Response.Headers[HeaderName] = correlationId;
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred. Quote the correlation id when contacting your administrator.",
            };
            var service = context.RequestServices.GetService<IProblemDetailsService>();
            if (service is null || !await service.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem }))
            {
                await context.Response.WriteAsJsonAsync(problem);
            }
        }
    }

    [GeneratedRegex("^[A-Za-z0-9._:-]{8,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidId();
}
