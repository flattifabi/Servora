using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Automation.Engine;
using Servora.Application.Automation.Model;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Automation;

namespace Servora.Application.Automation.Services;

/// <summary>
/// <see cref="IFlowExecutionService"/> implementation used by business rules, the REST API and MCP tools: lists
/// manually startable flows, starts runs with validated inputs and reports run status.
/// </summary>
/// <param name="dbFactory">The database factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="starter">The run starter.</param>
/// <param name="audit">The audit service.</param>
public sealed class FlowExecutionService(IAppDbContextFactory dbFactory, ICurrentUser currentUser, IFlowRunStarter starter, IAuditService audit) : IFlowExecutionService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<FlowSummary>> ListStartableAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Automation.Execute, cancellationToken))
        {
            return [];
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<Flow>().AsNoTracking()
            .Where(f => f.IsActive && f.TriggerType == FlowTriggerType.Manual)
            .OrderBy(f => f.Name)
            .Take(200)
            .Select(f => new FlowSummary(f.Id, f.Name, f.Description, f.IsActive))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> StartAsync(Guid flowId, IReadOnlyDictionary<string, object?> inputs, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Automation.Execute, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        Flow? flow;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            flow = await db.Set<Flow>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == flowId, cancellationToken);
        }

        if (flow is null)
        {
            return Result<Guid>.NotFound("The flow was not found.");
        }

        if (!flow.IsActive)
        {
            return Result<Guid>.Invalid($"The flow '{flow.Name}' is not active.");
        }

        var validated = FlowInputValidator.Validate(FlowJson.DeserializeInputs(flow.InputsJson), inputs);
        if (!validated.Succeeded)
        {
            return Result<Guid>.From(validated);
        }

        var result = await starter.StartAsync(
            new FlowStartRequest
            {
                FlowId = flowId,
                TriggerType = FlowTriggerType.Manual,
                Inputs = validated.Value,
                Trigger = new JsonObject { ["startedBy"] = currentUser.DisplayName, ["actorType"] = currentUser.ActorType.ToString() },
                Summary = $"Started by {currentUser.DisplayName}",
            },
            cancellationToken);
        if (result.Succeeded)
        {
            await audit.WriteAsync(new AuditEntry("automation", "flow.started") { TargetType = "flow", TargetId = flowId.ToString(), TargetName = flow.Name, Message = $"Run {result.Value}" }, cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<Result<FlowRunSummary>> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Automation.Read, cancellationToken)
            && !await currentUser.HasPermissionAsync(Permissions.Automation.Execute, cancellationToken))
        {
            return Result<FlowRunSummary>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var run = await db.Set<FlowRun>().AsNoTracking()
            .Where(r => r.Id == runId)
            .Select(r => new { r.Id, r.FlowId, r.FlowName, r.Status, r.QueuedAt, r.StartedAt, r.CompletedAt, r.Error })
            .FirstOrDefaultAsync(cancellationToken);
        return run is null
            ? Result<FlowRunSummary>.NotFound("The run was not found.")
            : Result<FlowRunSummary>.Success(new FlowRunSummary(run.Id, run.FlowId, run.FlowName, run.Status.ToString(), run.StartedAt ?? run.QueuedAt, run.CompletedAt, run.Error));
    }
}
