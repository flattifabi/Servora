namespace Servora.Application.Automation.Model;

/// <summary>Data pill based condition of an "If" step (all or any of the rules must match).</summary>
public sealed class FlowCondition
{
    /// <summary>Gets or sets a value indicating whether any rule (OR) instead of all rules (AND) must match.</summary>
    public bool MatchAny { get; set; }

    /// <summary>Gets or sets the rules. An empty list always matches.</summary>
    public List<FlowConditionRule> Rules { get; set; } = [];
}
