namespace Servora.Application.Conditions;

/// <summary>
/// A boolean expression over record fields, built in the UI with the condition builder and stored as JSON. Used by
/// list filters, business rules, flows, SLAs, notifications, reports and MCP queries. An empty group always matches.
/// </summary>
public sealed class ConditionGroup
{
    /// <summary>Gets or sets how rules and nested groups are combined.</summary>
    public ConditionLogic Logic { get; set; } = ConditionLogic.And;

    /// <summary>Gets or sets the rules.</summary>
    public List<ConditionRule> Rules { get; set; } = [];

    /// <summary>Gets or sets nested groups.</summary>
    public List<ConditionGroup> Groups { get; set; } = [];

    /// <summary>Gets a value indicating whether the group contains no rules at all (and therefore matches everything).</summary>
    public bool IsEmpty => Rules.Count == 0 && Groups.All(g => g.IsEmpty);
}
