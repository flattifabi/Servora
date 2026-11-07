using Servora.Application.Automation.Actions;
using Servora.Application.Automation.Model;
using Servora.Application.Tables;
using Servora.Domain.Automation;

namespace Servora.Application.Automation.Services;

/// <summary>
/// Computes the data pills available to a step: trigger data (record fields of the trigger table), inputs, variables
/// set by earlier steps and the outputs of steps that run before the target step (preceding siblings and ancestors'
/// preceding siblings – steps in other branches are not guaranteed to have run).
/// </summary>
public static class FlowDataPillBuilder
{
    /// <summary>Builds the pills for a step.</summary>
    /// <param name="model">The flow definition.</param>
    /// <param name="targetStepId">The step being configured (<see langword="null"/> = after the last step).</param>
    /// <param name="descriptors">The action descriptors.</param>
    /// <param name="tableLookup">Resolves a table definition by name.</param>
    /// <returns>The pills.</returns>
    public static IReadOnlyList<FlowDataPill> Build(FlowDefinitionModel model, string? targetStepId, IReadOnlyList<FlowActionDescriptor> descriptors, Func<string, TableDefinition?> tableLookup)
    {
        var pills = new List<FlowDataPill>();
        AddTriggerPills(model, tableLookup, pills);
        foreach (var input in model.Inputs.Where(i => !string.IsNullOrWhiteSpace(i.Name)))
        {
            pills.Add(new FlowDataPill($"inputs.{input.Name}", string.IsNullOrWhiteSpace(input.Label) ? input.Name : input.Label, "Inputs"));
        }

        var preceding = new List<FlowStepDefinition>();
        CollectPreceding(model.Steps, targetStepId, preceding);
        foreach (var step in preceding.Where(s => s.Type == SetVariableAction.TypeKey))
        {
            var name = step.GetConfig("name")?.Trim();
            if (SetVariableAction.IsValidName(name) && pills.All(p => p.Path != $"vars.{name}"))
            {
                pills.Add(new FlowDataPill($"vars.{name}", name!, "Variables"));
            }
        }

        foreach (var step in preceding)
        {
            var descriptor = descriptors.FirstOrDefault(d => d.Type == step.Type);
            if (descriptor is null)
            {
                continue;
            }

            var group = $"{step.Name} ({step.Key})";
            foreach (var output in descriptor.Outputs)
            {
                var path = $"steps.{step.Key}.{output.Key}";
                pills.Add(new FlowDataPill(path, output.Label, group));
                if (output.TableFieldKey is not null && step.GetConfig(output.TableFieldKey) is { Length: > 0 } tableName && tableLookup(tableName) is { } table)
                {
                    pills.AddRange(table.Fields.Take(80).Select(f => new FlowDataPill($"{path}.{f.Name}", $"{output.Label} › {f.Label}", group)));
                }
            }

            if (step.ContinueOnError)
            {
                pills.Add(new FlowDataPill($"steps.{step.Key}.error", "Error message (if failed)", group));
            }
        }

        pills.Add(new FlowDataPill("run.id", "Run id", "Run"));
        pills.Add(new FlowDataPill("run.startedAt", "Run started at", "Run"));
        pills.Add(new FlowDataPill("flow.name", "Flow name", "Run"));
        return pills;
    }

    private static void AddTriggerPills(FlowDefinitionModel model, Func<string, TableDefinition?> tableLookup, List<FlowDataPill> pills)
    {
        switch (model.TriggerType)
        {
            case FlowTriggerType.RecordCreated or FlowTriggerType.RecordUpdated or FlowTriggerType.RecordCreatedOrUpdated:
                pills.Add(new FlowDataPill("trigger.recordId", "Record id", "Trigger"));
                pills.Add(new FlowDataPill("trigger.table", "Table", "Trigger"));
                pills.Add(new FlowDataPill("trigger.changeType", "Change type", "Trigger"));
                pills.Add(new FlowDataPill("trigger.actor.name", "Changed by", "Trigger"));
                if (model.TriggerTable is { Length: > 0 } tableName && tableLookup(tableName) is { } table)
                {
                    pills.AddRange(table.Fields.Take(120).Select(f => new FlowDataPill($"trigger.record.{f.Name}", $"Record › {f.Label}", "Trigger record")));
                }

                break;
            case FlowTriggerType.Schedule:
                pills.Add(new FlowDataPill("trigger.scheduledAt", "Scheduled time", "Trigger"));
                pills.Add(new FlowDataPill("trigger.firedAt", "Fired at", "Trigger"));
                break;
            case FlowTriggerType.Webhook:
                pills.Add(new FlowDataPill("trigger.body", "Request body (JSON)", "Trigger"));
                pills.Add(new FlowDataPill("trigger.receivedAt", "Received at", "Trigger"));
                break;
            case FlowTriggerType.DataSourcePoll:
                pills.Add(new FlowDataPill("trigger.response.json", "Response JSON", "Trigger"));
                pills.Add(new FlowDataPill("trigger.response.body", "Response text", "Trigger"));
                pills.Add(new FlowDataPill("trigger.response.statusCode", "HTTP status", "Trigger"));
                break;
            default:
                pills.Add(new FlowDataPill("trigger.startedBy", "Started by", "Trigger"));
                break;
        }
    }

    /// <summary>Collects the steps that run before the target (depth-first), returning whether the target was found.</summary>
    private static bool CollectPreceding(List<FlowStepDefinition> steps, string? targetStepId, List<FlowStepDefinition> result)
    {
        foreach (var step in steps)
        {
            if (step.Id == targetStepId)
            {
                return true;
            }

            var mark = result.Count;
            foreach (var branch in step.Branches)
            {
                var branchResult = new List<FlowStepDefinition>();
                if (CollectPreceding(branch.Steps, targetStepId, branchResult))
                {
                    // The target lives inside this branch: the branching step itself ran, plus the branch's earlier steps.
                    result.Add(step);
                    result.AddRange(branchResult);
                    return true;
                }
            }

            result.Insert(mark, step);
        }

        return targetStepId is null;
    }
}
