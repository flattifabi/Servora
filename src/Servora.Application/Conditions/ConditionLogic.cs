namespace Servora.Application.Conditions;

/// <summary>How the rules and sub-groups of a <see cref="ConditionGroup"/> are combined.</summary>
public enum ConditionLogic
{
    /// <summary>All rules must match.</summary>
    And = 0,

    /// <summary>At least one rule must match.</summary>
    Or = 1,
}
