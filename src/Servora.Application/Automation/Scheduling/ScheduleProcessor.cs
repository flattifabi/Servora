using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Servora.Application.Automation.Engine;
using Servora.Application.Automation.Model;
using Servora.Application.Automation.Services;
using Servora.Application.Common;
using Servora.Application.Integration.DataSources;
using Servora.Application.Integration.Imports;
using Servora.Domain.Automation;
using Servora.Domain.Integration;

namespace Servora.Application.Automation.Scheduling;

/// <summary>
/// One scheduler tick: fires due schedule triggers (idempotency key = scheduled time, so a tick that runs twice never
/// starts a flow twice), polls due data source endpoints (optionally only firing on changed responses), runs due
/// imports and removes expired history. Called by the scheduler background service as a system actor.
/// </summary>
/// <param name="dbFactory">The database factory.</param>
/// <param name="starter">The run starter.</param>
/// <param name="executor">The data source executor.</param>
/// <param name="imports">The import processor.</param>
/// <param name="clock">The clock.</param>
/// <param name="options">The engine options.</param>
/// <param name="logger">The logger.</param>
public sealed class ScheduleProcessor(
    IAppDbContextFactory dbFactory,
    IFlowRunStarter starter,
    DataSourceExecutor executor,
    ImportProcessor imports,
    IClock clock,
    IOptions<FlowEngineOptions> options,
    ILogger<ScheduleProcessor> logger)
{
    private const int BatchSize = 50;

    /// <summary>Processes all due schedules.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of fired schedules.</returns>
    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken)
    {
        var fired = 0;
        var now = clock.UtcNow;
        List<Flow> dueFlows;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            dueFlows = await db.Set<Flow>().AsNoTracking()
                .Where(f => f.IsActive && f.NextRunAt != null && f.NextRunAt <= now
                    && (f.TriggerType == FlowTriggerType.Schedule || f.TriggerType == FlowTriggerType.DataSourcePoll))
                .OrderBy(f => f.NextRunAt)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
        }

        foreach (var flow in dueFlows)
        {
            try
            {
                fired += await FireFlowAsync(flow, now, cancellationToken) ? 1 : 0;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Scheduled trigger of flow {Flow} failed.", flow.Name);
                await AdvanceFlowAsync(flow, now, null, cancellationToken);
            }
        }

        List<ImportSet> dueImports;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            dueImports = await db.Set<ImportSet>().AsNoTracking()
                .Where(s => s.ScheduleEnabled && s.NextRunAt != null && s.NextRunAt <= now)
                .OrderBy(s => s.NextRunAt)
                .Take(10)
                .ToListAsync(cancellationToken);
        }

        foreach (var set in dueImports)
        {
            // Advance first so a slow or failing import never fires repeatedly.
            var next = ImportSetService.ComputeNextRun(set, now);
            await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
            {
                await db.Set<ImportSet>().Where(s => s.Id == set.Id).ExecuteUpdateAsync(setters => setters.SetProperty(s => s.NextRunAt, next), cancellationToken);
            }

            try
            {
                var result = await imports.RunFromDataSourceAsync(set, cancellationToken);
                fired++;
                if (!result.Succeeded)
                {
                    logger.LogWarning("Scheduled import {ImportSet} failed: {Error}", set.Name, result.Error);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Scheduled import {ImportSet} failed.", set.Name);
            }
        }

        return fired;
    }

    /// <summary>Deletes completed runs, execution logs and import runs older than the retention period (bounded batches).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of deleted entries.</returns>
    public async Task<int> CleanupAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.UtcNow.AddDays(-Math.Max(1, options.Value.RetentionDays));
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var runs = await db.Set<FlowRun>()
            .Where(r => r.CompletedAt != null && r.CompletedAt < cutoff)
            .OrderBy(r => r.CompletedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        var deleted = 0;
        foreach (var run in runs)
        {
            var steps = await db.Set<FlowRunStep>().Where(s => s.RunId == run.Id).Take(2000).ToListAsync(cancellationToken);
            db.Set<FlowRunStep>().RemoveRange(steps);
            db.Set<FlowRun>().Remove(run);
            deleted += steps.Count + 1;
        }

        var logs = await db.Set<DataSourceExecutionLog>().Where(l => l.StartedAt < cutoff).OrderBy(l => l.StartedAt).Take(1000).ToListAsync(cancellationToken);
        db.Set<DataSourceExecutionLog>().RemoveRange(logs);
        var importRuns = await db.Set<ImportRun>().Where(r => r.StartedAt < cutoff).OrderBy(r => r.StartedAt).Take(50).ToListAsync(cancellationToken);
        db.Set<ImportRun>().RemoveRange(importRuns);
        await db.SaveChangesAsync(cancellationToken);
        return deleted + logs.Count + importRuns.Count;
    }

    private async Task<bool> FireFlowAsync(Flow flow, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var scheduledAt = flow.NextRunAt!.Value;
        var trigger = new JsonObject
        {
            ["scheduledAt"] = scheduledAt.ToString("O", CultureInfo.InvariantCulture),
            ["firedAt"] = now.ToString("O", CultureInfo.InvariantCulture),
            ["cron"] = flow.CronExpression,
        };
        string? pollHash = null;
        if (flow.TriggerType == FlowTriggerType.DataSourcePoll)
        {
            if (flow.PollDataSourceId is not { } dataSourceId || string.IsNullOrWhiteSpace(flow.PollEndpointKey))
            {
                await AdvanceFlowAsync(flow, now, null, cancellationToken);
                return false;
            }

            var parameters = FlowJson.TryDeserialize<Dictionary<string, string>>(flow.PollParametersJson) ?? [];
            var response = await executor.ExecuteDetailedAsync(dataSourceId, flow.PollEndpointKey, parameters, isTest: false, cancellationToken);
            if (!response.Succeeded || response.Value!.StatusCode is < 200 or >= 300)
            {
                logger.LogWarning("Poll of flow {Flow} failed: {Error}", flow.Name, response.Error ?? $"HTTP {response.Value?.StatusCode}");
                await AdvanceFlowAsync(flow, now, null, cancellationToken);
                return false;
            }

            var body = response.Value.MappedBody ?? response.Value.Body;
            pollHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
            if (flow.PollOnlyOnChange && string.Equals(pollHash, flow.LastPollHash, StringComparison.Ordinal))
            {
                await AdvanceFlowAsync(flow, now, pollHash, cancellationToken, fired: false);
                return false;
            }

            var responseNode = new JsonObject { ["statusCode"] = response.Value.StatusCode, ["body"] = body.Length > 100_000 ? body[..100_000] : body };
            try
            {
                responseNode["json"] = JsonNode.Parse(body, documentOptions: new JsonDocumentOptions { MaxDepth = 64 });
            }
            catch (JsonException)
            {
                // Not JSON: only the text body is available to the flow.
            }

            trigger["response"] = responseNode;
        }

        var started = await starter.StartAsync(
            new FlowStartRequest
            {
                FlowId = flow.Id,
                TriggerType = flow.TriggerType,
                IdempotencyKey = $"cron:{scheduledAt.UtcTicks}",
                Trigger = trigger,
                Summary = flow.TriggerType == FlowTriggerType.Schedule ? $"Schedule {flow.CronExpression}" : $"Poll {flow.PollEndpointKey} (changed)",
                StartedBy = "Scheduler",
            },
            cancellationToken);
        if (!started.Succeeded)
        {
            logger.LogWarning("Scheduled flow {Flow} could not be started: {Error}", flow.Name, started.Error);
        }

        await AdvanceFlowAsync(flow, now, pollHash, cancellationToken);
        return started.Succeeded;
    }

    private async Task AdvanceFlowAsync(Flow flow, DateTimeOffset now, string? pollHash, CancellationToken cancellationToken, bool fired = true)
    {
        // Bulk update: scheduler bookkeeping must not bump the concurrency version of a flow open in the designer.
        var next = FlowService.ComputeNextRun(flow, now);
        var lastTriggered = fired ? now : flow.LastTriggeredAt;
        var hash = pollHash ?? flow.LastPollHash;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Set<Flow>()
            .Where(f => f.Id == flow.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(f => f.NextRunAt, next)
                    .SetProperty(f => f.LastTriggeredAt, lastTriggered)
                    .SetProperty(f => f.LastPollHash, hash),
                cancellationToken);
    }
}
