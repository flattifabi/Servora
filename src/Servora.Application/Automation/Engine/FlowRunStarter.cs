using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Domain.Automation;

namespace Servora.Application.Automation.Engine;

/// <summary>Default <see cref="IFlowRunStarter"/>: snapshots the flow definition into a new run and queues it.</summary>
/// <param name="dbFactory">The database factory.</param>
/// <param name="queue">The run queue.</param>
/// <param name="clock">The clock.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="logger">The logger.</param>
public sealed class FlowRunStarter(IAppDbContextFactory dbFactory, IFlowRunQueue queue, IClock clock, ICurrentUser currentUser, ILogger<FlowRunStarter> logger) : IFlowRunStarter
{
    /// <inheritdoc />
    public async Task<Result<Guid>> StartAsync(FlowStartRequest request, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var flow = await db.Set<Flow>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == request.FlowId, cancellationToken);
        if (flow is null)
        {
            return Result<Guid>.NotFound("The flow was not found.");
        }

        if (!flow.IsActive && !request.IsTest)
        {
            return Result<Guid>.Invalid($"The flow '{flow.Name}' is not active.");
        }

        var runId = Guid.CreateVersion7();
        var key = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? $"run:{runId:N}" : request.IdempotencyKey.Trim();
        if (key.Length > 200)
        {
            key = key[..200];
        }

        var existing = await db.Set<FlowRun>().AsNoTracking()
            .Where(r => r.FlowId == flow.Id && r.IdempotencyKey == key)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is { } existingId)
        {
            return Result<Guid>.Success(existingId);
        }

        var now = clock.UtcNow;
        var startedBy = request.StartedBy ?? currentUser.DisplayName;
        var context = FlowRunContext.Create(
            FlowRedactor.Redact(request.Trigger) as JsonObject,
            FlowRedactor.Redact(request.Inputs) as JsonObject,
            new JsonObject
            {
                ["id"] = runId.ToString(),
                ["startedBy"] = startedBy,
                ["startedAt"] = now.ToString("O", CultureInfo.InvariantCulture),
                ["isTest"] = request.IsTest,
                ["triggerType"] = request.TriggerType.ToString(),
            },
            new JsonObject { ["id"] = flow.Id.ToString(), ["name"] = flow.Name, ["version"] = flow.DefinitionVersion });

        var run = new FlowRun
        {
            Id = runId,
            FlowId = flow.Id,
            FlowName = flow.Name,
            FlowVersion = flow.DefinitionVersion,
            Status = FlowRunStatus.Queued,
            TriggerType = request.TriggerType,
            TriggerSummary = FlowRedactor.Truncate(request.Summary, 500),
            TriggerTable = request.TriggerTable,
            TriggerRecordId = request.TriggerRecordId,
            IdempotencyKey = key,
            ParentRunId = request.ParentRunId,
            Depth = request.Depth,
            EventDepth = request.EventDepth,
            IsTest = request.IsTest,
            StartedBy = FlowRedactor.Truncate(startedBy, 256),
            StepsJson = flow.StepsJson,
            ContextJson = context.Serialize(),
            QueuedAt = now,
        };
        db.Set<FlowRun>().Add(run);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Another trigger delivery with the same idempotency key won the race.
            logger.LogDebug(ex, "Duplicate start of flow {FlowId} with key {Key} ignored.", flow.Id, key);
            await using var retryDb = await dbFactory.CreateDbContextAsync(cancellationToken);
            var winner = await retryDb.Set<FlowRun>().AsNoTracking()
                .Where(r => r.FlowId == flow.Id && r.IdempotencyKey == key)
                .Select(r => (Guid?)r.Id)
                .FirstOrDefaultAsync(cancellationToken);
            return winner is { } winnerId ? Result<Guid>.Success(winnerId) : Result<Guid>.Failure("The run could not be created.");
        }

        queue.Enqueue(run.Id);
        return Result<Guid>.Success(run.Id);
    }
}
