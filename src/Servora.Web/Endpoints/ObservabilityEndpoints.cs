using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Observability;
using Servora.Web.Security;

namespace Servora.Web.Endpoints;

/// <summary>HTTP endpoints of the T-09 Observability &amp; Analytics module.</summary>
/// <remarks>
/// Owned by the module's task. Only the owning task edits this file. Downloads (log export, log files, audit CSV) are
/// plain GET endpoints used by links in the admin pages; the application services re-check permissions and audit them.
/// </remarks>
public static class ObservabilityEndpoints
{
    /// <summary>Maps the module endpoints.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapObservabilityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/observability/logs/export", ExportLogsAsync)
            .RequireAuthorization(PermissionPolicy.For(Permissions.Observability.ReadLogs))
            .ExcludeFromDescription();
        app.MapGet("/observability/logs/files/{fileName}", DownloadLogFileAsync)
            .RequireAuthorization(PermissionPolicy.For(Permissions.Observability.ReadLogs))
            .ExcludeFromDescription();
        app.MapGet("/observability/audit/export", ExportAuditAsync)
            .RequireAuthorization(PermissionPolicy.For(Permissions.Observability.ReadAudit))
            .ExcludeFromDescription();

        var api = app.MapGroup("/api/v1/observability").WithTags("Observability");
        api.MapGet("/health", GetHealthAsync)
            .RequireAuthorization(PermissionPolicy.For(Permissions.Observability.ReadHealth))
            .WithSummary("Detailed system health (checks, process, request metrics).");
        api.MapGet("/metrics", GetMetrics)
            .RequireAuthorization(PermissionPolicy.For(Permissions.Observability.ReadHealth))
            .WithSummary("HTTP request metrics aggregated per minute (window 1..1440 minutes).");
        return app;
    }

    private static async Task<IResult> ExportLogsAsync(
        ILoggingAdminService logs,
        [FromQuery] string? level,
        [FromQuery] string? category,
        [FromQuery] string? search,
        [FromQuery] string? correlationId,
        CancellationToken cancellationToken)
    {
        var query = new LogQuery
        {
            MinLevel = Enum.TryParse<LogLevel>(level, ignoreCase: true, out var parsed) ? parsed : null,
            Category = category,
            Search = search,
            CorrelationId = correlationId,
            Take = LogQuery.MaxTake,
        };
        var result = await logs.ExportAsync(query, cancellationToken);
        return result.Succeeded
            ? Results.File(result.Value!, "application/x-ndjson", $"servora-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.jsonl")
            : ToProblem(result);
    }

    private static async Task<IResult> DownloadLogFileAsync(string fileName, ILoggingAdminService logs, CancellationToken cancellationToken)
    {
        var result = await logs.OpenFileAsync(fileName, cancellationToken);
        return result.Succeeded ? Results.File(result.Value!, "application/octet-stream", fileName) : ToProblem(result);
    }

    private static async Task<IResult> ExportAuditAsync(
        IAuditLogService audit,
        [FromQuery] string? category,
        [FromQuery] string? action,
        [FromQuery] string? actor,
        [FromQuery] string? outcome,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var query = new AuditQuery
        {
            Category = category,
            Action = action,
            Actor = actor,
            Outcome = Enum.TryParse<AuditOutcome>(outcome, ignoreCase: true, out var parsedOutcome) ? parsedOutcome : null,
            From = ParseDate(from),
            To = ParseDate(to),
            Search = search,
        };
        var result = await audit.ExportCsvAsync(query, cancellationToken);
        return result.Succeeded
            ? Results.File(result.Value!, "text/csv; charset=utf-8", $"servora-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv")
            : ToProblem(result);
    }

    private static async Task<IResult> GetHealthAsync(ISystemHealthService health, CancellationToken cancellationToken)
    {
        var result = await health.GetReportAsync(cancellationToken: cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : ToProblem(result);
    }

    private static IResult GetMetrics(IRequestMetrics metrics, [FromQuery] int? minutes) =>
        Results.Ok(metrics.GetSnapshot(TimeSpan.FromMinutes(Math.Clamp(minutes ?? 60, 1, 1440))));

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : null;

    private static IResult ToProblem(Result result) => result.ErrorKind switch
    {
        ResultErrorKind.Forbidden => Results.Problem(result.Error, statusCode: StatusCodes.Status403Forbidden),
        ResultErrorKind.NotFound => Results.Problem(result.Error, statusCode: StatusCodes.Status404NotFound),
        ResultErrorKind.Validation => Results.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest),
        _ => Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity),
    };
}
