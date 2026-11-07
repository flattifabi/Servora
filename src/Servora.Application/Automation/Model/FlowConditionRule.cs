namespace Servora.Application.Automation.Model;

/// <summary>One comparison of a flow condition: <c>{{left}} operator {{right}}</c>. Both sides are templates.</summary>
public sealed class FlowConditionRule
{
    /// <summary>Gets or sets the left operand template, typically a data pill.</summary>
    public string Left { get; set; } = string.Empty;

    /// <summary>Gets or sets the operator.</summary>
    public FlowConditionOperator Operator { get; set; } = FlowConditionOperator.Equals;

    /// <summary>Gets or sets the right operand template.</summary>
    public string? Right { get; set; }
}
