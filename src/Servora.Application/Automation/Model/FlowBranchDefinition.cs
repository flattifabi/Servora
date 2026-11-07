namespace Servora.Application.Automation.Model;

/// <summary>A branch (lane) of a step, for example the "then" lane of a condition or the "approved" lane of an approval.</summary>
public sealed class FlowBranchDefinition
{
    /// <summary>Gets or sets the branch key returned by the action.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the display label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the steps of the branch.</summary>
    public List<FlowStepDefinition> Steps { get; set; } = [];
}
