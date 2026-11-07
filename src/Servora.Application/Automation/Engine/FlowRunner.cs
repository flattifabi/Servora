using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Servora.Application.Automation.Actions;
using Servora.Application.Automation.Model;
using Servora.Application.Automation.Templates;
using Servora.Application.Common;
using Servora.Domain.Automation;

namespace Servora.Application.Automation.Engine;

/// <summary>
/// Executes one flow run segment: replays completed steps (taking the recorded branches), executes the remaining steps
/// with retries and backoff, persists progress after every step and stops at the first waiting step. Waiting runs are
/// continued later by calling <see cref="ExecuteAsync"/> again (resume). Runs as the flow's run-as identity, a named
/// system actor (<c>Flow: &lt;name&gt;</c>), so record changes are attributed to the flow in audit fields.
/// </summary>
/// <param name="dbFactory">The database factory.</param>
/// <param name="registry">The action registry.</param>
/// <param name="queue">The run queue (to wake parent runs).</param>
/// <param name="actorContext">The actor context of the execution scope.</param>
/// <param name="clock">The clock.</param>
/// <param name="options">The engine options.</param>
/// <param name="logger">The logger.</param>
public sealed class FlowRunner(
    IAppDbContextFactory dbFactory,
    FlowActionRegistry registry,
    IFlowRunQueue queue,
    ActorContext actorContext,
    IClock clock,
    IOptions<FlowEngineOptions> options,
    ILogger<FlowRunner> logger)
{
    private const int SnapshotLength = 16_000;

    /// <summary>Executes (or resumes) a run until it completes, fails, is cancelled or waits.</summary>
    /// <param name="runId">The run identifier.</param>
    /// <param name="cancellationToken">Cancelled when the run is cancelled or the host stops.</param>
    /// <returns>The status after this segment.</returns>
    public async Task<FlowRunStatus> ExecuteAsync(Guid runId, CancellationToken cancellationToken)
    {
        FlowRun? snapshot;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            snapshot = await db.Set<FlowRun>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
        }

        if (snapshot is null || snapshot.Status is FlowRunStatus.Succeeded or FlowRunStatus.Failed or FlowRunStatus.Cancelled)
        {
            return snapshot?.Status ?? FlowRunStatus.Failed;
        }

        actorContext.Set(ActorInfo.SystemNamed($"Flow: {snapshot.FlowName}"), runId.ToString("N"));
        actorContext.EventDepth = snapshot.EventDepth;

        if (snapshot.CancelRequested)
        {
            await FinishAsync(runId, FlowRunStatus.Cancelled, "Cancelled before execution.", null, cancellationToken);
            return FlowRunStatus.Cancelled;
        }

        var now = clock.UtcNow;
        await UpdateRunAsync(runId, r =>
        {
            r.Status = FlowRunStatus.Running;
            r.StartedAt ??= now;
            r.ResumeAt = null;
        }, cancellationToken);

        var state = new FlowExecutionState
        {
            RunId = runId,
            FlowId = snapshot.FlowId,
            FlowName = snapshot.FlowName,
            Depth = snapshot.Depth,
            IsTest = snapshot.IsTest,
            Context = FlowRunContext.Parse(snapshot.ContextJson),
            Sequence = snapshot.StepCount,
        };

        var steps = FlowJson.DeserializeSteps(snapshot.StepsJson);
        FlowSegmentOutcome outcome;
        try
        {
            outcome = await RunStepsAsync(steps, state, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (await IsCancelRequestedAsync(runId, CancellationToken.None))
            {
                outcome = FlowSegmentOutcome.Cancelled;
            }
            else
            {
                // Host shutdown: requeue so the run continues (replaying completed steps) after the restart.
                await UpdateRunAsync(runId, r =>
                {
                    r.Status = FlowRunStatus.Queued;
                    r.ContextJson = state.Context.Serialize();
                    r.StepCount = state.Sequence;
                }, CancellationToken.None);
                return FlowRunStatus.Queued;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Flow run {RunId} of {Flow} failed unexpectedly.", runId, snapshot.FlowName);
            state.Error = "The flow engine failed unexpectedly. See the application log for details.";
            outcome = FlowSegmentOutcome.Failed;
        }

        switch (outcome)
        {
            case FlowSegmentOutcome.Waiting:
                await UpdateRunAsync(runId, r =>
                {
                    r.Status = FlowRunStatus.Waiting;
                    r.ResumeAt = state.ResumeAt ?? clock.UtcNow.AddMinutes(1);
                    r.WaitingStepId = state.WaitStepId;
                    r.WaitReason = FlowRedactor.Truncate(state.WaitReason, 500);
                    r.ContextJson = state.Context.Serialize();
                    r.StepCount = state.Sequence;
                }, CancellationToken.None);
                return FlowRunStatus.Waiting;
            case FlowSegmentOutcome.Completed:
                await FinishAsync(runId, FlowRunStatus.Succeeded, null, state, CancellationToken.None);
                return FlowRunStatus.Succeeded;
            case FlowSegmentOutcome.Cancelled:
                await FinishAsync(runId, FlowRunStatus.Cancelled, "Cancelled by user.", state, CancellationToken.None);
                return FlowRunStatus.Cancelled;
            default:
                await FinishAsync(runId, FlowRunStatus.Failed, state.Error ?? "The flow failed.", state, CancellationToken.None);
                return FlowRunStatus.Failed;
        }
    }

    private async Task<FlowSegmentOutcome> RunStepsAsync(IReadOnlyList<FlowStepDefinition> steps, FlowExecutionState state, CancellationToken cancellationToken)
    {
        foreach (var step in steps)
        {
            if (step.Disabled)
            {
                continue;
            }

            if (state.Context.IsCompleted(step.Id))
            {
                // Replay: the step ran in an earlier segment; follow the branch it chose.
                if (step.FindBranch(state.Context.GetBranch(step.Id)) is { } replayBranch)
                {
                    var replayOutcome = await RunStepsAsync(replayBranch.Steps, state, cancellationToken);
                    if (replayOutcome != FlowSegmentOutcome.Completed)
                    {
                        return replayOutcome;
                    }
                }

                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (await IsCancelRequestedAsync(state.RunId, cancellationToken))
            {
                return FlowSegmentOutcome.Cancelled;
            }

            if (++state.Executions > Math.Max(1, options.Value.MaxStepExecutionsPerRun))
            {
                state.Error = $"The run exceeded the maximum of {options.Value.MaxStepExecutionsPerRun} step executions.";
                return FlowSegmentOutcome.Failed;
            }

            var action = registry.Find(step.Type);
            if (action is null)
            {
                state.Error = $"Step '{step.Name}' uses the unknown action '{step.Type}'.";
                return FlowSegmentOutcome.Failed;
            }

            var result = await ExecuteStepAsync(step, action, state, cancellationToken);
            switch (result.Outcome)
            {
                case FlowActionOutcome.Succeeded:
                    state.Context.SetStepOutput(StepKey(step), result.Output);
                    state.Context.MarkCompleted(step.Id, result.Branch);
                    await PersistProgressAsync(state, cancellationToken);
                    if (step.FindBranch(result.Branch) is { } branch)
                    {
                        var branchOutcome = await RunStepsAsync(branch.Steps, state, cancellationToken);
                        if (branchOutcome != FlowSegmentOutcome.Completed)
                        {
                            return branchOutcome;
                        }
                    }

                    break;
                case FlowActionOutcome.Waiting:
                    state.Context.SetWaitState(step.Id, result.WaitState);
                    state.WaitStepId = step.Id;
                    state.ResumeAt = result.ResumeAt;
                    state.WaitReason = $"{step.Name}: {result.WaitReason}";
                    return FlowSegmentOutcome.Waiting;
                default:
                    if (step.ContinueOnError)
                    {
                        var output = new JsonObject { ["error"] = result.Error, ["failed"] = true, ["output"] = result.Output?.DeepClone() };
                        state.Context.SetStepOutput(StepKey(step), output);
                        state.Context.MarkCompleted(step.Id, null);
                        await PersistProgressAsync(state, cancellationToken);
                        break;
                    }

                    state.Error = FlowRedactor.Truncate($"Step '{step.Name}' failed: {result.Error}", 2000);
                    return FlowSegmentOutcome.Failed;
            }
        }

        return FlowSegmentOutcome.Completed;
    }

    private async Task<FlowActionResult> ExecuteStepAsync(FlowStepDefinition step, IFlowAction action, FlowExecutionState state, CancellationToken cancellationToken)
    {
        var isResume = state.Context.IsWaiting(step.Id);
        var waitState = state.Context.GetWaitState(step.Id)?.DeepClone();
        var maxAttempts = 1 + Math.Clamp(step.RetryCount, 0, 5);
        var inputSnapshot = FlowRedactor.ToStoredJson(BuildInputSnapshot(step, action, state.Context), SnapshotLength);
        FlowActionResult result = FlowActionResult.Fail("The step did not run.");

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var row = await StartStepRowAsync(step, state, attempt, inputSnapshot, reuseWaitingRow: isResume && attempt == 1, cancellationToken);
            var watch = Stopwatch.StartNew();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.Value.StepTimeoutSeconds, 1, 3600)));
            var context = new FlowActionContext
            {
                RunId = state.RunId,
                FlowId = state.FlowId,
                FlowName = state.FlowName,
                Depth = state.Depth,
                IsTest = state.IsTest,
                Step = step,
                Data = state.Context,
                Now = clock.UtcNow,
                IsResume = isResume,
                WaitState = waitState,
            };

            try
            {
                result = await action.ExecuteAsync(context, timeout.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await CompleteStepRowAsync(row, FlowStepStatus.Skipped, watch.ElapsedMilliseconds, null, "Cancelled.", CancellationToken.None);
                throw;
            }
            catch (OperationCanceledException)
            {
                result = FlowActionResult.Fail($"The step timed out after {options.Value.StepTimeoutSeconds} seconds.", retryable: true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Step {Step} ({Type}) of flow run {RunId} threw an exception.", step.Name, step.Type, state.RunId);
                result = FlowActionResult.Fail("The step failed unexpectedly. See the application log for details.");
            }

            var status = result.Outcome switch
            {
                FlowActionOutcome.Succeeded => FlowStepStatus.Succeeded,
                FlowActionOutcome.Waiting => FlowStepStatus.Waiting,
                _ => FlowStepStatus.Failed,
            };
            var output = result.Outcome == FlowActionOutcome.Waiting
                ? new JsonObject { ["waitingFor"] = result.WaitReason, ["resumeAt"] = result.ResumeAt?.ToString("O") }
                : result.Output is null && result.Branch is null ? null : WithBranch(result.Output, result.Branch);
            await CompleteStepRowAsync(row, status, watch.ElapsedMilliseconds, FlowRedactor.ToStoredJson(output, SnapshotLength), result.Error, CancellationToken.None);

            if (result.Outcome != FlowActionOutcome.Failed || !result.Retryable || attempt == maxAttempts)
            {
                return result;
            }

            var delaySeconds = Math.Min(300, Math.Max(0, step.RetryDelaySeconds) * Math.Pow(2, attempt - 1));
            if (delaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }

            isResume = false;
        }

        return result;
    }

    private static JsonNode? WithBranch(JsonNode? output, string? branch)
    {
        if (branch is null)
        {
            return output;
        }

        var copy = output is JsonObject obj ? (JsonObject)obj.DeepClone() : new JsonObject { ["value"] = output?.DeepClone() };
        copy["branch"] = branch;
        return copy;
    }

    private static string StepKey(FlowStepDefinition step) => string.IsNullOrWhiteSpace(step.Key) ? step.Id : step.Key;

    private static JsonObject BuildInputSnapshot(FlowStepDefinition step, IFlowAction action, FlowRunContext context)
    {
        var snapshot = new JsonObject();
        foreach (var field in action.Descriptor.Fields)
        {
            var raw = step.GetConfig(field.Key);
            if (raw is null)
            {
                continue;
            }

            snapshot[field.Key] = field.Kind is FlowActionFieldKind.Text or FlowActionFieldKind.Multiline or FlowActionFieldKind.Number
                ? FlowTemplate.Render(raw, context.Data)
                : raw;
        }

        return snapshot;
    }

    private async Task<Guid> StartStepRowAsync(FlowStepDefinition step, FlowExecutionState state, int attempt, string? input, bool reuseWaitingRow, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (reuseWaitingRow)
        {
            var waiting = await db.Set<FlowRunStep>()
                .Where(s => s.RunId == state.RunId && s.StepId == step.Id && s.Status == FlowStepStatus.Waiting)
                .OrderByDescending(s => s.Sequence)
                .FirstOrDefaultAsync(cancellationToken);
            if (waiting is not null)
            {
                waiting.Status = FlowStepStatus.Running;
                await db.SaveChangesAsync(cancellationToken);
                return waiting.Id;
            }
        }

        var row = new FlowRunStep
        {
            RunId = state.RunId,
            StepId = step.Id,
            StepKey = FlowRedactor.Truncate(StepKey(step), 64)!,
            StepName = FlowRedactor.Truncate(string.IsNullOrWhiteSpace(step.Name) ? step.Type : step.Name, 200)!,
            ActionType = FlowRedactor.Truncate(step.Type, 100)!,
            Sequence = ++state.Sequence,
            Attempt = attempt,
            Status = FlowStepStatus.Running,
            StartedAt = clock.UtcNow,
            InputJson = input,
        };
        db.Set<FlowRunStep>().Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    private async Task CompleteStepRowAsync(Guid rowId, FlowStepStatus status, long elapsedMs, string? output, string? error, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Set<FlowRunStep>().FirstOrDefaultAsync(s => s.Id == rowId, cancellationToken);
        if (row is null)
        {
            return;
        }

        var now = clock.UtcNow;
        row.Status = status;
        row.CompletedAt = status == FlowStepStatus.Waiting ? null : now;
        row.DurationMs = status == FlowStepStatus.Waiting ? elapsedMs : Math.Max(elapsedMs, (long)(now - row.StartedAt).TotalMilliseconds);
        row.OutputJson = output;
        row.Error = FlowRedactor.Truncate(error, 2000);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task PersistProgressAsync(FlowExecutionState state, CancellationToken cancellationToken) =>
        await UpdateRunAsync(state.RunId, r =>
        {
            r.ContextJson = state.Context.Serialize();
            r.StepCount = state.Sequence;
        }, cancellationToken);

    private async Task<bool> IsCancelRequestedAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<FlowRun>().AsNoTracking().Where(r => r.Id == runId).Select(r => r.CancelRequested).FirstOrDefaultAsync(cancellationToken);
    }

    private async Task UpdateRunAsync(Guid runId, Action<FlowRun> mutate, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var run = await db.Set<FlowRun>().FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
        if (run is null)
        {
            return;
        }

        mutate(run);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task FinishAsync(Guid runId, FlowRunStatus status, string? error, FlowExecutionState? state, CancellationToken cancellationToken)
    {
        Guid? parentId = null;
        await UpdateRunAsync(runId, r =>
        {
            r.Status = status;
            r.CompletedAt = clock.UtcNow;
            r.ResumeAt = null;
            r.WaitingStepId = null;
            r.WaitReason = null;
            r.Error = FlowRedactor.Truncate(error, 2000);
            if (state is not null)
            {
                r.ContextJson = state.Context.Serialize();
                r.StepCount = state.Sequence;
            }

            parentId = r.ParentRunId;
        }, cancellationToken);

        if (parentId is { } parent)
        {
            // Wake the waiting parent immediately instead of waiting for its next poll.
            var woke = false;
            await UpdateRunAsync(parent, p =>
            {
                if (p.Status == FlowRunStatus.Waiting)
                {
                    p.ResumeAt = clock.UtcNow;
                    woke = true;
                }
            }, cancellationToken);
            if (woke)
            {
                queue.Enqueue(parent);
            }
        }
    }
}
