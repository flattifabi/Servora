using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Servora.Application.Automation.Engine;
using Servora.Application.Automation.Model;
using Servora.Application.Common;
using Servora.Application.Tables;
using Servora.Domain.Automation;

namespace Servora.Application.Automation.Actions;

/// <summary>
/// Starts another flow as a sub-flow (inputs from data pills) and optionally waits for its completion. The child's
/// variables become the step output. Nesting is limited by <see cref="FlowEngineOptions.MaxSubflowDepth"/>.
/// </summary>
/// <param name="starter">The run starter.</param>
/// <param name="dbFactory">The database factory.</param>
/// <param name="options">The engine options.</param>
public sealed class SubflowAction(IFlowRunStarter starter, IAppDbContextFactory dbFactory, IOptions<FlowEngineOptions> options) : IFlowAction
{
    /// <summary>The type key.</summary>
    public const string TypeKey = "flow.subflow";

    /// <inheritdoc />
    public FlowActionDescriptor Descriptor { get; } = new(TypeKey, "Run sub-flow", "Flow logic", "workflow", "accent", "Run another flow with inputs and use its variables.")
    {
        Fields =
        [
            new FlowActionField("flowId", "Flow", FlowActionFieldKind.Flow) { Required = true, SupportsPills = false },
            new FlowActionField("inputs", "Inputs", FlowActionFieldKind.KeyValues),
            new FlowActionField("wait", "Wait for completion", FlowActionFieldKind.Select) { DefaultValue = "yes", SupportsPills = false, Options = [new FieldChoice("yes", "Yes"), new FieldChoice("no", "No (fire and forget)")] },
        ],
        Outputs = [new FlowActionOutput("runId", "Sub-flow run id"), new FlowActionOutput("status", "Status"), new FlowActionOutput("vars", "Sub-flow variables")],
        CanWait = true,
    };

    /// <inheritdoc />
    public async Task<FlowActionResult> ExecuteAsync(FlowActionContext context, CancellationToken cancellationToken)
    {
        var poll = TimeSpan.FromSeconds(Math.Clamp(options.Value.SubflowPollSeconds, 5, 3600));
        if (context.IsResume && FlowValues.ParseGuid(context.WaitState?["childRunId"] is JsonValue v ? Templates.FlowTemplate.ToText(v) : null) is { } childId)
        {
            return await CheckChildAsync(childId, context, poll, cancellationToken);
        }

        if (FlowValues.ParseGuid(context.GetRaw("flowId")) is not { } flowId)
        {
            return FlowActionResult.Fail("No sub-flow is configured.");
        }

        if (flowId == context.FlowId)
        {
            return FlowActionResult.Fail("A flow cannot call itself as a sub-flow.");
        }

        if (context.Depth + 1 > options.Value.MaxSubflowDepth)
        {
            return FlowActionResult.Fail($"Sub-flow nesting exceeds the maximum depth of {options.Value.MaxSubflowDepth}.");
        }

        var inputs = new JsonObject();
        foreach (var pair in CallDataSourceAction.ResolveParameters(context, context.GetRaw("inputs")))
        {
            inputs[pair.Key] = pair.Value;
        }

        var started = await starter.StartAsync(
            new FlowStartRequest
            {
                FlowId = flowId,
                TriggerType = FlowTriggerType.Manual,
                IdempotencyKey = $"sub:{context.RunId:N}:{context.Step.Id}",
                Inputs = inputs,
                Trigger = new JsonObject { ["parentRunId"] = context.RunId.ToString(), ["parentFlow"] = context.FlowName },
                Summary = $"Sub-flow of {context.FlowName}",
                ParentRunId = context.RunId,
                Depth = context.Depth + 1,
                IsTest = context.IsTest,
                StartedBy = $"Flow: {context.FlowName}",
            },
            cancellationToken);
        if (!started.Succeeded)
        {
            return FlowActionResult.Fail(started.Error ?? "The sub-flow could not be started.");
        }

        if (string.Equals(context.GetRaw("wait"), "no", StringComparison.OrdinalIgnoreCase))
        {
            return FlowActionResult.Success(new JsonObject { ["runId"] = started.Value.ToString(), ["status"] = nameof(FlowRunStatus.Queued) });
        }

        return FlowActionResult.Wait(context.Now + poll, "Waiting for sub-flow", new JsonObject { ["childRunId"] = started.Value.ToString() });
    }

    private async Task<FlowActionResult> CheckChildAsync(Guid childId, FlowActionContext context, TimeSpan poll, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var child = await db.Set<FlowRun>().AsNoTracking()
            .Where(r => r.Id == childId)
            .Select(r => new { r.Status, r.Error, r.ContextJson })
            .FirstOrDefaultAsync(cancellationToken);
        if (child is null)
        {
            return FlowActionResult.Fail("The sub-flow run no longer exists.");
        }

        switch (child.Status)
        {
            case FlowRunStatus.Succeeded:
                var childContext = FlowRunContext.Parse(child.ContextJson);
                return FlowActionResult.Success(new JsonObject
                {
                    ["runId"] = childId.ToString(),
                    ["status"] = child.Status.ToString(),
                    ["vars"] = childContext.Variables.DeepClone(),
                });
            case FlowRunStatus.Failed:
            case FlowRunStatus.Cancelled:
                return FlowActionResult.Fail($"The sub-flow {child.Status.ToString().ToLowerInvariant()}: {child.Error ?? "no details"}", output: new JsonObject { ["runId"] = childId.ToString(), ["status"] = child.Status.ToString() });
            default:
                return FlowActionResult.Wait(context.Now + poll, "Waiting for sub-flow", context.WaitState);
        }
    }
}
