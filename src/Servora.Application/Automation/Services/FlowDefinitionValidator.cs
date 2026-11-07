using System.Text.RegularExpressions;
using Servora.Application.Automation.Actions;
using Servora.Application.Automation.Model;
using Servora.Application.Automation.Scheduling;
using Servora.Domain.Automation;

namespace Servora.Application.Automation.Services;

/// <summary>
/// Validates flow definitions. Structural rules (limits, unique ids / keys, known actions, valid names) apply to every
/// save; completeness rules (required fields, trigger configuration) apply when a flow is active or activated.
/// </summary>
public static partial class FlowDefinitionValidator
{
    /// <summary>The maximum number of flow inputs.</summary>
    public const int MaxInputs = 50;

    /// <summary>Normalizes a definition before validation: assigns missing ids and keys and syncs branch lanes with the action.</summary>
    /// <param name="model">The model.</param>
    /// <param name="registry">The action registry.</param>
    public static void Normalize(FlowDefinitionModel model, FlowActionRegistry registry)
    {
        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in FlowJson.Flatten(model.Steps))
        {
            if (string.IsNullOrWhiteSpace(step.Id) || step.Id.Length > 64 || !usedIds.Add(step.Id))
            {
                step.Id = FlowStepDefinition.NewId();
                usedIds.Add(step.Id);
            }

            step.Key = step.Key?.Trim() ?? string.Empty;
            if (!IsValidKey(step.Key) || usedKeys.Contains(step.Key))
            {
                step.Key = NextKey(step.Type, usedKeys);
            }

            usedKeys.Add(step.Key);
            step.Name = string.IsNullOrWhiteSpace(step.Name) ? registry.Find(step.Type)?.Descriptor.Name ?? step.Type : step.Name.Trim();
            step.RetryCount = Math.Clamp(step.RetryCount, 0, 5);
            step.RetryDelaySeconds = Math.Clamp(step.RetryDelaySeconds, 0, 300);

            if (registry.Find(step.Type)?.Descriptor is { } descriptor)
            {
                foreach (var branch in descriptor.Branches)
                {
                    if (step.FindBranch(branch.Key) is null)
                    {
                        step.Branches.Add(new FlowBranchDefinition { Key = branch.Key, Label = branch.Label });
                    }
                }

                step.Branches.RemoveAll(b => descriptor.Branches.All(d => !string.Equals(d.Key, b.Key, StringComparison.OrdinalIgnoreCase)) && b.Steps.Count == 0);
            }
        }
    }

    /// <summary>Validates a definition.</summary>
    /// <param name="model">The model.</param>
    /// <param name="registry">The action registry.</param>
    /// <param name="forActivation">Whether completeness rules apply.</param>
    /// <returns>The issues (empty when valid).</returns>
    public static IReadOnlyList<string> Validate(FlowDefinitionModel model, FlowActionRegistry registry, bool forActivation)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            issues.Add("The flow name is required.");
        }
        else if (model.Name.Trim().Length > 200)
        {
            issues.Add("The flow name must not exceed 200 characters.");
        }

        var all = FlowJson.Flatten(model.Steps).ToList();
        if (all.Count > FlowJson.MaxSteps)
        {
            issues.Add($"A flow can contain at most {FlowJson.MaxSteps} steps.");
        }

        if (FlowJson.GetDepth(model.Steps) > FlowJson.MaxNesting)
        {
            issues.Add($"Branches can be nested at most {FlowJson.MaxNesting} levels deep.");
        }

        if (all.Select(s => s.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != all.Count)
        {
            issues.Add("Step keys must be unique.");
        }

        foreach (var step in all)
        {
            var action = registry.Find(step.Type);
            if (action is null)
            {
                issues.Add($"Step '{step.Name}' uses the unknown action '{step.Type}'.");
                continue;
            }

            if (step.Config.Count > 50 || step.Config.Values.Any(v => v is { Length: > 50_000 }))
            {
                issues.Add($"Step '{step.Name}' has too much configuration.");
            }

            if (step.Type == SetVariableAction.TypeKey && !SetVariableAction.IsValidName(step.GetConfig("name")?.Trim()) && forActivation)
            {
                issues.Add($"Step '{step.Name}': the variable name must start with a letter and contain only letters, digits and underscores.");
            }

            if (forActivation && !step.Disabled)
            {
                foreach (var field in action.Descriptor.Fields.Where(f => f.Required))
                {
                    var value = step.GetConfig(field.Key);
                    if (string.IsNullOrWhiteSpace(value) || value is "[]" or "{}")
                    {
                        issues.Add($"Step '{step.Name}': '{field.Label}' is required.");
                    }
                }
            }
        }

        var inputNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (model.Inputs.Count > MaxInputs)
        {
            issues.Add($"A flow can have at most {MaxInputs} inputs.");
        }

        foreach (var input in model.Inputs)
        {
            if (!IsValidKey(input.Name))
            {
                issues.Add($"The input name '{input.Name}' is invalid (letter first, then letters, digits, underscore).");
            }
            else if (!inputNames.Add(input.Name))
            {
                issues.Add($"The input '{input.Name}' is defined twice.");
            }
        }

        switch (model.TriggerType)
        {
            case FlowTriggerType.RecordCreated or FlowTriggerType.RecordUpdated or FlowTriggerType.RecordCreatedOrUpdated when forActivation && string.IsNullOrWhiteSpace(model.TriggerTable):
                issues.Add("Choose the table of the record trigger.");
                break;
            case FlowTriggerType.Schedule or FlowTriggerType.DataSourcePoll:
                if (!string.IsNullOrWhiteSpace(model.CronExpression) && !CronExpression.TryParse(model.CronExpression, out _, out var cronError))
                {
                    issues.Add(cronError!);
                }
                else if (forActivation && string.IsNullOrWhiteSpace(model.CronExpression))
                {
                    issues.Add("Enter the cron schedule of the trigger.");
                }

                if (!CronExpression.TryFindTimeZone(model.TimeZoneId, out _))
                {
                    issues.Add($"The time zone '{model.TimeZoneId}' is unknown.");
                }

                if (model.TriggerType == FlowTriggerType.DataSourcePoll && forActivation && (model.PollDataSourceId is null || string.IsNullOrWhiteSpace(model.PollEndpointKey)))
                {
                    issues.Add("Choose the data source endpoint to poll.");
                }

                break;
        }

        return issues;
    }

    /// <summary>Checks whether a step key / input name is valid.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    public static bool IsValidKey(string? key) => key is not null && KeyRegex().IsMatch(key);

    private static string NextKey(string? type, HashSet<string> used)
    {
        var baseName = (type ?? "step").Split('.').Last().ToLowerInvariant();
        baseName = KeyCleanupRegex().Replace(baseName, string.Empty);
        if (baseName.Length == 0 || !char.IsLetter(baseName[0]))
        {
            baseName = "step";
        }

        for (var i = 1; ; i++)
        {
            var candidate = $"{baseName}{i}";
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyRegex();

    [GeneratedRegex("[^a-z0-9_]", RegexOptions.CultureInvariant)]
    private static partial Regex KeyCleanupRegex();
}
