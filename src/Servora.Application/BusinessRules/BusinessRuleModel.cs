using Servora.Application.Conditions;
using Servora.Domain.BusinessRules;

namespace Servora.Application.BusinessRules;

/// <summary>A business rule as edited in the UI.</summary>
public sealed class BusinessRuleModel
{
    /// <summary>Gets or sets the identifier (empty for new rules).</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the technical table name.</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the rule is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Gets or sets when the rule runs.</summary>
    public BusinessRuleTiming Timing { get; set; } = BusinessRuleTiming.Before;

    /// <summary>Gets or sets a value indicating whether the rule runs on insert.</summary>
    public bool OnInsert { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the rule runs on update.</summary>
    public bool OnUpdate { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the rule runs on delete.</summary>
    public bool OnDelete { get; set; }

    /// <summary>Gets or sets the execution order.</summary>
    public int Order { get; set; } = 100;

    /// <summary>Gets or sets the condition.</summary>
    public ConditionGroup Condition { get; set; } = new();

    /// <summary>Gets or sets the actions.</summary>
    public List<BusinessRuleAction> Actions { get; set; } = [];

    /// <summary>Gets or sets the UTC time of the last change (read only).</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Gets or sets who changed the rule last (read only).</summary>
    public string? UpdatedBy { get; set; }
}
