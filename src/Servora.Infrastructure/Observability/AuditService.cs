using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Domain.Observability;

namespace Servora.Infrastructure.Observability;

/// <summary>
/// Persists audit events with a SHA-256 hash chain (each event hashes its content plus the previous hash) so that
/// modification or deletion of past events is detectable. Writes are serialized within the process.
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
public sealed class AuditService(IAppDbContextFactory dbFactory, ICurrentUser currentUser, IClock clock, ILogger<AuditService> logger) : IAuditService
{
    private static readonly SemaphoreSlim s_writeLock = new(1, 1);
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    /// <summary>Computes the chain hash of an event.</summary>
    /// <param name="auditEvent">The event (with <see cref="AuditEvent.PreviousHash"/> set).</param>
    /// <returns>The lower-case hex SHA-256 hash.</returns>
    public static string ComputeHash(AuditEvent auditEvent)
    {
        var canonical = string.Join('|',
            auditEvent.Id.ToString("N"),
            auditEvent.OccurredAt.ToUnixTimeMilliseconds(),
            auditEvent.Category,
            auditEvent.Action,
            (int)auditEvent.Outcome,
            auditEvent.ActorId,
            auditEvent.ActorName,
            auditEvent.ActorType,
            auditEvent.TargetType,
            auditEvent.TargetId,
            auditEvent.TargetName,
            auditEvent.Message,
            auditEvent.DetailsJson,
            auditEvent.IpAddress,
            auditEvent.CorrelationId,
            auditEvent.PreviousHash);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <inheritdoc />
    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        var auditEvent = new AuditEvent
        {
            OccurredAt = clock.UtcNow,
            Category = Truncate(entry.Category, 64),
            Action = Truncate(entry.Action, 128),
            Outcome = entry.Outcome,
            ActorId = currentUser.UserId?.ToString() ?? currentUser.ApiKeyId?.ToString(),
            ActorName = Truncate(entry.ActorNameOverride ?? currentUser.DisplayName, 256),
            ActorType = currentUser.ActorType.ToString().ToLowerInvariant(),
            TargetType = entry.TargetType,
            TargetId = entry.TargetId,
            TargetName = entry.TargetName is null ? null : Truncate(entry.TargetName, 256),
            Message = entry.Message is null ? null : Truncate(entry.Message, 2000),
            DetailsJson = entry.Details is null ? null : Truncate(JsonSerializer.Serialize(entry.Details, s_json), 16_000),
            IpAddress = currentUser.IpAddress,
            CorrelationId = currentUser.CorrelationId is { } c ? Truncate(c, 64) : null,
        };

        await s_writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            auditEvent.PreviousHash = await db.Set<AuditEvent>().AsNoTracking()
                .OrderByDescending(a => a.OccurredAt)
                .Select(a => a.Hash)
                .FirstOrDefaultAsync(cancellationToken);
            auditEvent.Hash = ComputeHash(auditEvent);
            db.Set<AuditEvent>().Add(auditEvent);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Auditing must never break the audited operation; the failure itself is logged.
            logger.LogError(ex, "Failed to write audit event {Category}/{Action}.", entry.Category, entry.Action);
        }
        finally
        {
            s_writeLock.Release();
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
