using System.Linq.Expressions;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Automation.Engine;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Automation;

namespace Servora.Application.Automation.Services;

/// <summary>Default <see cref="IFlowRunService"/>.</summary>
/// <param name="dbFactory">The database factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="queue">The run queue.</param>
/// <param name="starter">The run starter.</param>
/// <param name="audit">The audit service.</param>
/// <param name="clock">The clock.</param>
public sealed class FlowRunService(
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    IFlowRunQueue queue,
    IFlowRunStarter starter,
    IAuditService audit,
    IClock clock) : IFlowRunService
{
    private static readonly Expression<Func<FlowRun, FlowRunListItem>> s_projection = r => new FlowRunListItem(
        r.Id, r.FlowId, r.FlowName, r.Status, r.TriggerType, r.TriggerSummary, r.IsTest, r.QueuedAt, r.StartedAt, r.CompletedAt, r.StepCount, r.Error, r.WaitReason);

    private static readonly Func<FlowRun, FlowRunListItem> s_map = s_projection.Compile();

    /// <inheritdoc />
    public async Task<Result<PagedResult<FlowRunListItem>>> ListAsync(FlowRunQuery query, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Automation.Read, cancellationToken))
        {
            return Result<PagedResult<FlowRunListItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var runs = db.Set<FlowRun>().AsNoTracking();
        if (query.Status is { } status)
        {
            runs = runs.Where(r => r.Status == status);
        }

        if (query.FlowId is { } flowId)
        {
            runs = runs.Where(r => r.FlowId == flowId);
        }

        if (!query.IncludeTests)
        {
            runs = runs.Where(r => !r.IsTest);
        }

        if (!string.IsNullOrWhiteSpace(query.Page.Search))
        {
            var search = query.Page.Search.Trim();
            runs = Guid.TryParse(search, out var id)
                ? runs.Where(r => r.Id == id || r.TriggerRecordId == id)
                : runs.Where(r => r.FlowName.Contains(search) || (r.TriggerSummary != null && r.TriggerSummary.Contains(search)));
        }

        runs = (query.Page.SortBy, query.Page.SortDescending) switch
        {
            ("Flow", false) => runs.OrderBy(r => r.FlowName).ThenByDescending(r => r.QueuedAt),
            ("Flow", true) => runs.OrderByDescending(r => r.FlowName).ThenByDescending(r => r.QueuedAt),
            ("Status", false) => runs.OrderBy(r => r.Status).ThenByDescending(r => r.QueuedAt),
            ("Status", true) => runs.OrderByDescending(r => r.Status).ThenByDescending(r => r.QueuedAt),
            ("QueuedAt", false) => runs.OrderBy(r => r.QueuedAt),
            _ => runs.OrderByDescending(r => r.QueuedAt),
        };

        return Result<PagedResult<FlowRunListItem>>.Success(await runs.Select(s_projection).ToPagedResultAsync(query.Page, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<FlowRunDetail>> GetAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Automation.Read, cancellationToken))
        {
            return Result<FlowRunDetail>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var run = await db.Set<FlowRun>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
        if (run is null)
        {
            return Result<FlowRunDetail>.NotFound("The run was not found.");
        }

        var steps = await db.Set<FlowRunStep>().AsNoTracking()
            .Where(s => s.RunId == runId)
            .OrderBy(s => s.Sequence)
            .Take(1000)
            .Select(s => new FlowRunStepItem(s.Id, s.StepId, s.StepKey, s.StepName, s.ActionType, s.Sequence, s.Status, s.Attempt, s.StartedAt, s.CompletedAt, s.DurationMs, s.InputJson, s.OutputJson, s.Error))
            .ToListAsync(cancellationToken);
        var children = await db.Set<FlowRun>().AsNoTracking()
            .Where(r => r.ParentRunId == runId)
            .OrderBy(r => r.QueuedAt)
            .Select(r => r.Id)
            .Take(100)
            .ToListAsync(cancellationToken);
        var item = s_map(run);
        return Result<FlowRunDetail>.Success(new FlowRunDetail(
            item,
            steps,
            FlowRunContext.Parse(run.ContextJson).ToPublicJson(),
            run.ParentRunId,
            children,
            run.ResumeAt,
            run.FlowVersion,
            run.StartedBy,
            run.CancelRequested));
    }

    /// <inheritdoc />
    public async Task<Result> CancelAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Automation.Execute, cancellationToken)
            && !await currentUser.HasPermissionAsync(Permissions.Automation.Manage, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var run = await db.Set<FlowRun>().FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
        if (run is null)
        {
            return Result.NotFound("The run was not found.");
        }

        if (run.Status is FlowRunStatus.Succeeded or FlowRunStatus.Failed or FlowRunStatus.Cancelled)
        {
            return Result.Invalid("The run has already finished.");
        }

        var cancelled = new List<FlowRun> { run };
        var children = await db.Set<FlowRun>()
            .Where(r => r.ParentRunId == runId && (r.Status == FlowRunStatus.Queued || r.Status == FlowRunStatus.Waiting || r.Status == FlowRunStatus.Running))
            .Take(50)
            .ToListAsync(cancellationToken);
        cancelled.AddRange(children);
        var now = clock.UtcNow;
        foreach (var target in cancelled)
        {
            target.CancelRequested = true;
            if (target.Status is FlowRunStatus.Queued or FlowRunStatus.Waiting)
            {
                target.Status = FlowRunStatus.Cancelled;
                target.CompletedAt = now;
                target.ResumeAt = null;
                target.Error = $"Cancelled by {currentUser.DisplayName}.";
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        foreach (var target in cancelled)
        {
            queue.SignalCancel(target.Id);
        }

        await audit.WriteAsync(new AuditEntry("automation", "flow.run.cancelled") { TargetType = "flow_run", TargetId = runId.ToString(), TargetName = run.FlowName }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> RerunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Automation.Execute, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        FlowRun? run;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            run = await db.Set<FlowRun>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
        }

        if (run is null)
        {
            return Result<Guid>.NotFound("The run was not found.");
        }

        var context = FlowRunContext.Parse(run.ContextJson);
        var result = await starter.StartAsync(
            new FlowStartRequest
            {
                FlowId = run.FlowId,
                TriggerType = run.TriggerType,
                Trigger = context.Trigger.DeepClone() as JsonObject,
                Inputs = context.Inputs.DeepClone() as JsonObject,
                Summary = FlowRedactor.Truncate($"Re-run of {run.Id.ToString()[..8]}: {run.TriggerSummary}", 500),
                TriggerTable = run.TriggerTable,
                TriggerRecordId = run.TriggerRecordId,
                IsTest = run.IsTest,
            },
            cancellationToken);
        if (result.Succeeded)
        {
            await audit.WriteAsync(new AuditEntry("automation", "flow.started") { TargetType = "flow", TargetId = run.FlowId.ToString(), TargetName = run.FlowName, Message = $"Re-run of {run.Id}" }, cancellationToken);
        }

        return result;
    }
}
