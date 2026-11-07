using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Observability;

namespace Servora.Application.Observability;

/// <summary>Default <see cref="IAuditLogService"/>.</summary>
/// <param name="currentUser">The current actor.</param>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="audit">The audit writer.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
public sealed class AuditLogService(
    ICurrentUser currentUser,
    IAppDbContextFactory dbFactory,
    IAuditService audit,
    IClock clock,
    ILogger<AuditLogService> logger) : IAuditLogService
{
    /// <summary>Maximum number of rows in a CSV export.</summary>
    public const int MaxExportRows = 50_000;

    /// <summary>Number of events loaded per verification batch.</summary>
    public const int VerificationBatchSize = 2_000;

    /// <summary>Maximum number of events deleted per retention run.</summary>
    public const int PruneBatchSize = 5_000;

    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <inheritdoc />
    public async Task<Result<PagedResult<AuditEvent>>> QueryAsync(AuditQuery query, PageRequest page, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Observability.ReadAudit, cancellationToken))
        {
            return Result<PagedResult<AuditEvent>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var filtered = Apply(db.Set<AuditEvent>().AsNoTracking(), query, page.Search);
        var sorted = (page.SortBy, page.SortDescending) switch
        {
            ("Category", false) => filtered.OrderBy(a => a.Category).ThenByDescending(a => a.Sequence),
            ("Category", true) => filtered.OrderByDescending(a => a.Category).ThenByDescending(a => a.Sequence),
            ("Action", false) => filtered.OrderBy(a => a.Action).ThenByDescending(a => a.Sequence),
            ("Action", true) => filtered.OrderByDescending(a => a.Action).ThenByDescending(a => a.Sequence),
            ("ActorName", false) => filtered.OrderBy(a => a.ActorName).ThenByDescending(a => a.Sequence),
            ("ActorName", true) => filtered.OrderByDescending(a => a.ActorName).ThenByDescending(a => a.Sequence),
            ("Outcome", false) => filtered.OrderBy(a => a.Outcome).ThenByDescending(a => a.Sequence),
            ("Outcome", true) => filtered.OrderByDescending(a => a.Outcome).ThenByDescending(a => a.Sequence),
            ("OccurredAt" or "Sequence", false) => filtered.OrderBy(a => a.Sequence).ThenBy(a => a.OccurredAt),
            _ => filtered.OrderByDescending(a => a.Sequence).ThenByDescending(a => a.OccurredAt),
        };

        return Result<PagedResult<AuditEvent>>.Success(await sorted.ToPagedResultAsync(page, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<AuditEvent>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Observability.ReadAudit, cancellationToken))
        {
            return Result<AuditEvent>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var auditEvent = await db.Set<AuditEvent>().AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        return auditEvent is null ? Result<AuditEvent>.NotFound("The audit event was not found.") : Result<AuditEvent>.Success(auditEvent);
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> ExportCsvAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Observability.ReadAudit, cancellationToken))
        {
            return Result<byte[]>.Forbidden();
        }

        var builder = new StringBuilder();
        AuditCsvWriter.WriteHeader(builder);
        var rows = 0;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var events = Apply(db.Set<AuditEvent>().AsNoTracking(), query, null)
                .OrderByDescending(a => a.Sequence)
                .ThenByDescending(a => a.OccurredAt)
                .Take(MaxExportRows)
                .AsAsyncEnumerable();
            await foreach (var auditEvent in events.WithCancellation(cancellationToken))
            {
                AuditCsvWriter.WriteRow(builder, auditEvent);
                rows++;
            }
        }

        await audit.WriteAsync(new AuditEntry("audit", "audit.exported")
        {
            Message = $"{rows} audit event(s) exported as CSV.",
            Details = new { query.Category, query.Action, query.Actor, outcome = query.Outcome?.ToString(), query.From, query.To, query.Search, rows },
        }, cancellationToken);

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(builder.ToString());
        return Result<byte[]>.Success([.. preamble, .. body]);
    }

    /// <inheritdoc />
    public async Task<Result<AuditChainVerification>> VerifyChainAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Observability.ReadAudit, cancellationToken))
        {
            return Result<AuditChainVerification>.Forbidden();
        }

        var stopwatch = Stopwatch.StartNew();
        AuditChainVerifier verifier;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var anchor = await SystemSettingAccess.GetAsync(db, ObservabilitySettingKeys.AuditChainAnchor, cancellationToken);
            var anchorSequence = await SystemSettingAccess.GetIntAsync(db, ObservabilitySettingKeys.AuditChainAnchorSequence, 0, cancellationToken);
            verifier = new AuditChainVerifier(string.IsNullOrEmpty(anchor) ? null : anchor, anchorSequence);

            // Keyset pagination over the chain order keeps memory constant for arbitrarily large logs.
            long lastSequence = long.MinValue;
            while (true)
            {
                var batch = await db.Set<AuditEvent>().AsNoTracking()
                    .Where(a => a.Sequence > lastSequence)
                    .OrderBy(a => a.Sequence)
                    .ThenBy(a => a.OccurredAt)
                    .Take(VerificationBatchSize)
                    .ToListAsync(cancellationToken);
                foreach (var auditEvent in batch)
                {
                    verifier.Add(auditEvent);
                }

                if (batch.Count < VerificationBatchSize)
                {
                    break;
                }

                lastSequence = batch[^1].Sequence;
            }
        }

        stopwatch.Stop();
        var result = new AuditChainVerification(
            verifier.IsIntact,
            verifier.CheckedCount,
            verifier.IssueCount,
            verifier.Issues,
            verifier.FirstSequence,
            verifier.LastSequence,
            verifier.HeadHash,
            clock.UtcNow,
            stopwatch.ElapsedMilliseconds,
            currentUser.DisplayName);

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var stored = result with { Issues = result.Issues.Take(10).ToList() };
            await SystemSettingAccess.SetAsync(db, ObservabilitySettingKeys.AuditLastVerification, JsonSerializer.Serialize(stored, s_json), "Result of the last audit hash-chain verification.", "json", cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!result.IsIntact)
        {
            logger.LogWarning("Audit hash-chain verification found {IssueCount} integrity problem(s) in {Checked} events.", result.IssueCount, result.CheckedCount);
        }

        await audit.WriteAsync(new AuditEntry("audit", "audit.chain_verified", result.IsIntact ? AuditOutcome.Success : AuditOutcome.Failure)
        {
            Message = result.IsIntact
                ? $"Hash chain intact ({result.CheckedCount} events verified)."
                : $"Hash chain BROKEN: {result.IssueCount} problem(s) in {result.CheckedCount} events.",
            Details = new { result.CheckedCount, result.IssueCount, result.FirstSequence, result.LastSequence, result.HeadHash },
        }, cancellationToken);

        return Result<AuditChainVerification>.Success(result);
    }

    /// <inheritdoc />
    public async Task<Result<AuditChainVerification?>> GetLastVerificationAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Observability.ReadAudit, cancellationToken))
        {
            return Result<AuditChainVerification?>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var json = await SystemSettingAccess.GetAsync(db, ObservabilitySettingKeys.AuditLastVerification, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result<AuditChainVerification?>.Success(null);
        }

        try
        {
            return Result<AuditChainVerification?>.Success(JsonSerializer.Deserialize<AuditChainVerification>(json, s_json));
        }
        catch (JsonException)
        {
            return Result<AuditChainVerification?>.Success(null);
        }
    }

    /// <inheritdoc />
    public async Task<Result<AuditStatistics>> GetStatisticsAsync(int days = 14, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Observability.ReadAudit, cancellationToken))
        {
            return Result<AuditStatistics>.Forbidden();
        }

        days = Math.Clamp(days, 1, 90);
        var now = clock.UtcNow;
        var dayAgo = now.AddDays(-1);
        var periodStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddDays(-(days - 1));

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var set = db.Set<AuditEvent>().AsNoTracking();
        var total = await set.CountAsync(cancellationToken);
        var last24 = await set.Where(a => a.OccurredAt >= dayAgo).GroupBy(a => a.Outcome).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var byCategory = await set.Where(a => a.OccurredAt >= periodStart).GroupBy(a => a.Category)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var recent = await set.Where(a => a.OccurredAt >= periodStart)
            .OrderByDescending(a => a.Sequence)
            .Select(a => new { a.OccurredAt, a.Outcome })
            .Take(200_000)
            .ToListAsync(cancellationToken);
        var retention = await SystemSettingAccess.GetIntAsync(db, ObservabilitySettingKeys.AuditRetentionDays, 0, cancellationToken);

        var perDay = Enumerable.Range(0, days)
            .Select(i => DateOnly.FromDateTime(periodStart.UtcDateTime.AddDays(i)))
            .Select(day =>
            {
                var items = recent.Where(r => DateOnly.FromDateTime(r.OccurredAt.UtcDateTime) == day).ToList();
                return new AuditDayCount(day, items.Count(r => r.Outcome == AuditOutcome.Success), items.Count(r => r.Outcome == AuditOutcome.Failure), items.Count(r => r.Outcome == AuditOutcome.Denied));
            })
            .ToList();

        return Result<AuditStatistics>.Success(new AuditStatistics(
            total,
            last24.Sum(x => x.Count),
            last24.Where(x => x.Key == AuditOutcome.Failure).Sum(x => x.Count),
            last24.Where(x => x.Key == AuditOutcome.Denied).Sum(x => x.Count),
            byCategory.OrderByDescending(x => x.Count).Select(x => new NamedCount(x.Key, x.Count)).ToList(),
            perDay,
            retention));
    }

    /// <inheritdoc />
    public async Task<Result<AuditFacets>> GetFacetsAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Observability.ReadAudit, cancellationToken))
        {
            return Result<AuditFacets>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var set = db.Set<AuditEvent>().AsNoTracking();
        var categories = await set.Select(a => a.Category).Distinct().OrderBy(c => c).Take(200).ToListAsync(cancellationToken);
        var actions = await set.Select(a => a.Action).Distinct().OrderBy(c => c).Take(500).ToListAsync(cancellationToken);
        return Result<AuditFacets>.Success(new AuditFacets(categories, actions));
    }

    /// <inheritdoc />
    public async Task<Result> SetRetentionDaysAsync(int days, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Observability.ReadAudit, cancellationToken)
            || !await currentUser.HasPermissionAsync(Permissions.Configuration.ManageSettings, cancellationToken))
        {
            return Result.Forbidden();
        }

        if (days != 0 && days is < 30 or > 3650)
        {
            return Result.Invalid("Audit retention must be 0 (keep forever) or between 30 and 3650 days.");
        }

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            await SystemSettingAccess.SetAsync(db, ObservabilitySettingKeys.AuditRetentionDays, days.ToString(CultureInfo.InvariantCulture), "Days audit events are kept (0 = forever).", "int", cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        await audit.WriteAsync(new AuditEntry("config", "audit.retention.changed")
        {
            TargetType = "audit",
            Message = days == 0 ? "Audit retention set to keep events forever." : $"Audit retention set to {days} days.",
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<int> PruneAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Configuration.ManageSettings, cancellationToken))
        {
            return 0;
        }

        int deleted;
        long anchorSequence;
        int retention;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            retention = await SystemSettingAccess.GetIntAsync(db, ObservabilitySettingKeys.AuditRetentionDays, 0, cancellationToken);
            if (retention <= 0)
            {
                return 0;
            }

            var cutoff = clock.UtcNow.AddDays(-retention);
            var newest = await db.Set<AuditEvent>().AsNoTracking()
                .Where(a => a.OccurredAt < cutoff)
                .OrderBy(a => a.Sequence)
                .Select(a => new { a.Sequence, a.Hash })
                .Take(PruneBatchSize)
                .ToListAsync(cancellationToken);
            if (newest.Count == 0)
            {
                return 0;
            }

            var anchor = newest[^1];
            anchorSequence = anchor.Sequence;

            // Record the anchor first: the oldest retained event links to it, so pruning never looks like tampering.
            await SystemSettingAccess.SetAsync(db, ObservabilitySettingKeys.AuditChainAnchor, anchor.Hash, "Hash of the newest pruned audit event (chain anchor).", "string", cancellationToken);
            await SystemSettingAccess.SetAsync(db, ObservabilitySettingKeys.AuditChainAnchorSequence, anchor.Sequence.ToString(CultureInfo.InvariantCulture), "Sequence of the newest pruned audit event.", "int", cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            deleted = await db.Set<AuditEvent>().Where(a => a.Sequence <= anchorSequence).ExecuteDeleteAsync(cancellationToken);
        }

        await audit.WriteAsync(new AuditEntry("audit", "audit.pruned")
        {
            Message = $"{deleted} audit event(s) older than {retention} days were deleted by retention (up to sequence {anchorSequence}).",
        }, cancellationToken);
        logger.LogInformation("Audit retention deleted {Count} event(s) up to sequence {Sequence}.", deleted, anchorSequence);
        return deleted;
    }

    private static IQueryable<AuditEvent> Apply(IQueryable<AuditEvent> source, AuditQuery query, string? pageSearch)
    {
        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim();
            source = source.Where(a => a.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim();
            source = source.Where(a => a.Action.StartsWith(action));
        }

        if (!string.IsNullOrWhiteSpace(query.Actor))
        {
            var actor = query.Actor.Trim().ToLower();
            source = source.Where(a => a.ActorName.ToLower().Contains(actor) || (a.ActorId != null && a.ActorId == actor));
        }

        if (query.Outcome is { } outcome)
        {
            source = source.Where(a => a.Outcome == outcome);
        }

        if (query.From is { } from)
        {
            source = source.Where(a => a.OccurredAt >= from);
        }

        if (query.To is { } to)
        {
            source = source.Where(a => a.OccurredAt < to);
        }

        var search = string.IsNullOrWhiteSpace(pageSearch) ? query.Search : pageSearch;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            source = source.Where(a =>
                a.Action.ToLower().Contains(term)
                || a.ActorName.ToLower().Contains(term)
                || (a.Message != null && a.Message.ToLower().Contains(term))
                || (a.TargetName != null && a.TargetName.ToLower().Contains(term))
                || (a.TargetId != null && a.TargetId == term)
                || (a.CorrelationId != null && a.CorrelationId == term)
                || (a.IpAddress != null && a.IpAddress == term));
        }

        return source;
    }
}
