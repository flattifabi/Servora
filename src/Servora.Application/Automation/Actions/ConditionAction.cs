using System.Text.Json.Nodes;
using Servora.Application.Automation.Engine;
using Servora.Application.Automation.Model;

namespace Servora.Application.Automation.Actions;

/// <summary>If / else branch on a data pill condition.</summary>
public sealed class ConditionAction : IFlowAction
{
    /// <summary>The type key.</summary>
    public const string TypeKey = "flow.if";

    /// <summary>The branch executed when the condition matches.</summary>
    public const string ThenBranch = "then";

    /// <summary>The branch executed otherwise.</summary>
    public const string ElseBranch = "else";

    /// <inheritdoc />
    public FlowActionDescriptor Descriptor { get; } = new(TypeKey, "If / else", "Flow logic", "git-branch", "warning", "Run different steps depending on a condition.")
    {
        Fields = [new FlowActionField("condition", "Condition", FlowActionFieldKind.Condition) { Required = true }],
        Outputs = [new FlowActionOutput("result", "Condition result")],
        Branches = [new FlowActionBranch(ThenBranch, "If true", "success"), new FlowActionBranch(ElseBranch, "Otherwise", "neutral")],
    };

    /// <inheritdoc />
    public Task<FlowActionResult> ExecuteAsync(FlowActionContext context, CancellationToken cancellationToken)
    {
        var condition = FlowJson.TryDeserialize<FlowCondition>(context.GetRaw("condition"));
        var matched = FlowConditionEvaluator.Evaluate(condition, context.Render);
        return Task.FromResult(FlowActionResult.Success(new JsonObject { ["result"] = matched }, matched ? ThenBranch : ElseBranch));
    }
}
