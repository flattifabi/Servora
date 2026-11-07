using Servora.Application.Automation.Services;
using Servora.Application.Common;
using Servora.Web.Modules;

namespace Servora.Web.Endpoints;

/// <summary>HTTP endpoints of the T-08 Automation &amp; Integration module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class AutomationEndpoints
{
    /// <summary>Maps the module endpoints.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapAutomationEndpoints(this IEndpointRouteBuilder app)
    {
        // Inbound webhook trigger. Authenticated by the flow's secret (shared secret header or HMAC body signature),
        // not by a user session, and rate limited per client IP.
        app.MapPost("/api/v1/automation/webhooks/{flowId:guid}", ReceiveWebhookAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .RequireRateLimiting(AutomationWebModule.WebhookRateLimitPolicy)
            .WithName("AutomationWebhook")
            .WithTags("Automation")
            .WithSummary("Starts a webhook-triggered flow.");
        return app;
    }

    private static async Task<IResult> ReceiveWebhookAsync(Guid flowId, HttpRequest request, FlowWebhookService webhooks, CancellationToken cancellationToken)
    {
        if (request.ContentLength > FlowWebhookService.MaxBodyBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > FlowWebhookService.MaxBodyBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            buffer.Write(chunk, 0, read);
        }

        var result = await webhooks.ReceiveAsync(
            flowId,
            request.Headers["X-Servora-Webhook-Secret"].FirstOrDefault(),
            request.Headers["X-Servora-Signature"].FirstOrDefault(),
            buffer.ToArray(),
            request.ContentType,
            request.Headers["Idempotency-Key"].FirstOrDefault(),
            cancellationToken);

        return result.ErrorKind switch
        {
            ResultErrorKind.None => Results.Accepted(value: new { runId = result.Value }),
            ResultErrorKind.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
            ResultErrorKind.Validation => Results.BadRequest(new { error = result.Error }),
            _ => Results.UnprocessableEntity(new { error = result.Error }),
        };
    }
}
