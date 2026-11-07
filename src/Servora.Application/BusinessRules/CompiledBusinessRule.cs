using Servora.Application.Conditions;
using Servora.Application.Events;
using Servora.Domain.BusinessRules;

namespace Servora.Application.BusinessRules;

/// <summary>An active business rule with parsed condition and actions, cached per table.</summary>
/// <param name="Id">The rule identifier.</param>
/// <param name="Name">The rule name.</param>
/// <param name="Timing">When the rule runs.</param>
/// <param name="OnInsert">Whether the rule runs on insert.</param>
/// <param name="OnUpdate">Whether the rule runs on update.</param>
/// <param name="OnDelete">Whether the rule runs on delete.</param>
/// <param name="Condition">The condition.</param>
/// <param name="Actions">The actions.</param>
public sealed record CompiledBusinessRule(
    Guid Id,
    string Name,
    BusinessRuleTiming Timing,
    bool OnInsert,
    bool OnUpdate,
    bool OnDelete,
    ConditionGroup Condition,
    IReadOnlyList<BusinessRuleAction> Actions)
{
    /// <summary>Checks whether the rule runs for a change type.</summary>
    /// <param name="changeType">The change type.</param>
    /// <returns><see langword="true"/> when the rule applies.</returns>
    public bool AppliesTo(RecordChangeType changeType) => changeType switch
    {
        RecordChangeType.Created => OnInsert,
        RecordChangeType.Updated => OnUpdate,
        _ => OnDelete,
    };
}
