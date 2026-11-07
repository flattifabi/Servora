using Servora.Application.Automation.Actions;
using Servora.Application.Automation.Model;

namespace Servora.Tests.Unit.Automation;

/// <summary>Builders for flow step definitions used by the engine tests.</summary>
internal static class TestSteps
{
    /// <summary>Creates a step.</summary>
    /// <param name="key">The data pill key (also the name).</param>
    /// <param name="type">The action type.</param>
    /// <param name="config">The configuration values.</param>
    /// <returns>The step.</returns>
    public static FlowStepDefinition Step(string key, string type, params (string Key, string? Value)[] config) => new()
    {
        Key = key,
        Name = key,
        Type = type,
        Config = config.ToDictionary(c => c.Key, c => c.Value),
    };

    /// <summary>Creates a "set variable" step.</summary>
    /// <param name="key">The step key.</param>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The value template.</param>
    /// <returns>The step.</returns>
    public static FlowStepDefinition Set(string key, string name, string value) => Step(key, SetVariableAction.TypeKey, ("name", name), ("value", value));

    /// <summary>Creates an "if" step with then / else lanes.</summary>
    /// <param name="key">The step key.</param>
    /// <param name="condition">The condition.</param>
    /// <param name="then">The steps of the "then" lane.</param>
    /// <param name="otherwise">The steps of the "else" lane.</param>
    /// <returns>The step.</returns>
    public static FlowStepDefinition If(string key, FlowCondition condition, IEnumerable<FlowStepDefinition> then, IEnumerable<FlowStepDefinition> otherwise)
    {
        var step = Step(key, ConditionAction.TypeKey, ("condition", FlowJson.Serialize(condition)));
        step.Branches =
        [
            new FlowBranchDefinition { Key = ConditionAction.ThenBranch, Label = "If true", Steps = then.ToList() },
            new FlowBranchDefinition { Key = ConditionAction.ElseBranch, Label = "Otherwise", Steps = otherwise.ToList() },
        ];
        return step;
    }

    /// <summary>Creates a single-rule condition.</summary>
    /// <param name="left">The left template.</param>
    /// <param name="op">The operator.</param>
    /// <param name="right">The right template.</param>
    /// <returns>The condition.</returns>
    public static FlowCondition Rule(string left, FlowConditionOperator op, string? right) =>
        new() { Rules = [new FlowConditionRule { Left = left, Operator = op, Right = right }] };
}
