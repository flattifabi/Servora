using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Servora.Application.Automation.Engine;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Automation;

namespace Servora.Application.Automation.Services;

/// <summary>
/// Authenticates inbound webhook calls and starts the flow. A call is accepted when it carries either an HMAC-SHA256
/// signature of the raw body (<c>X-Servora-Signature: sha256=&lt;hex&gt;</c>) or the shared secret
/// (<c>X-Servora-Webhook-Secret</c>), compared in constant time. Unknown, inactive or non-webhook flows and wrong secrets
/// are indistinguishable to the caller.
/// </summary>
/// <param name="dbFactory">The database factory.</param>
/// <param name="secrets">The secret store.</param>
/// <param name="starter">The run starter.</param>
/// <param name="actorContext">The actor context.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
public sealed class FlowWebhookService(
    IAppDbContextFactory dbFactory,
    ISecretStore secrets,
    IFlowRunStarter starter,
    ActorContext actorContext,
    IClock clock,
    ILogger<FlowWebhookService> logger)
{
    /// <summary>The maximum accepted body size in bytes.</summary>
    public const int MaxBodyBytes = 256 * 1024;

    /// <summary>Computes the signature header value for a body.</summary>
    /// <param name="secret">The secret.</param>
    /// <param name="body">The raw body.</param>
    /// <returns>The value <c>sha256=&lt;hex&gt;</c>.</returns>
    public static string ComputeSignature(string secret, ReadOnlySpan<byte> body) =>
        "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body));

    /// <summary>Handles a webhook call.</summary>
    /// <param name="flowId">The flow.</param>
    /// <param name="presentedSecret">The shared secret header value.</param>
    /// <param name="signature">The signature header value.</param>
    /// <param name="body">The raw body.</param>
    /// <param name="contentType">The content type.</param>
    /// <param name="idempotencyKey">The optional <c>Idempotency-Key</c> header value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The run identifier; <see cref="ResultErrorKind.Forbidden"/> for any authentication failure.</returns>
    public async Task<Result<Guid>> ReceiveAsync(Guid flowId, string? presentedSecret, string? signature, byte[] body, string? contentType, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        Flow? flow;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            flow = await db.Set<Flow>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == flowId && f.IsActive && f.TriggerType == FlowTriggerType.Webhook, cancellationToken);
        }

        var secret = flow is null ? null : await secrets.GetAsync(FlowSecretNames.Webhook(flowId), cancellationToken);
        if (flow is null || string.IsNullOrEmpty(secret) || !IsAuthentic(secret, presentedSecret, signature, body))
        {
            logger.LogWarning("Rejected webhook call for flow {FlowId}: unknown flow, inactive trigger or invalid credentials.", flowId);
            return Result<Guid>.Forbidden("The webhook call was rejected.");
        }

        if (body.Length > MaxBodyBytes)
        {
            return Result<Guid>.Invalid("The request body is too large.");
        }

        actorContext.Set(ActorInfo.SystemNamed($"Webhook: {flow.Name}"));
        var text = Encoding.UTF8.GetString(body);
        JsonNode? payload = null;
        if (text.Length > 0)
        {
            try
            {
                payload = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { MaxDepth = 32 });
            }
            catch (JsonException)
            {
                payload = JsonValue.Create(text.Length > 10_000 ? text[..10_000] : text);
            }
        }

        var key = string.IsNullOrWhiteSpace(idempotencyKey) ? null : "wh:" + new string(idempotencyKey.Trim().Where(c => !char.IsControl(c)).Take(100).ToArray());
        return await starter.StartAsync(
            new FlowStartRequest
            {
                FlowId = flowId,
                TriggerType = FlowTriggerType.Webhook,
                IdempotencyKey = key,
                Trigger = new JsonObject
                {
                    ["body"] = payload,
                    ["contentType"] = contentType is { Length: <= 200 } ? contentType : null,
                    ["receivedAt"] = clock.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                },
                Summary = "Inbound webhook",
                StartedBy = "Webhook",
            },
            cancellationToken);
    }

    private static bool IsAuthentic(string secret, string? presentedSecret, string? signature, byte[] body)
    {
        if (!string.IsNullOrWhiteSpace(signature))
        {
            var expected = Encoding.ASCII.GetBytes(ComputeSignature(secret, body));
            var actual = Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant());
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }

        if (string.IsNullOrEmpty(presentedSecret))
        {
            return false;
        }

        // Compare hashes so the comparison time does not depend on the secret length.
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(presentedSecret));
        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }
}
