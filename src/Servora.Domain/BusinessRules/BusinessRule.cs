using Servora.Domain.Common;

namespace Servora.Domain.BusinessRules;

/// <summary>
/// A declarative business rule (the equivalent of a ServiceNow <c>sys_script</c> entry without code): when a record of
/// <see cref="TableName"/> is inserted, updated or deleted and the condition matches, the configured actions run.
/// </summary>
public class BusinessRule : AuditableEntity
{
    /// <summary>Gets or sets the rule name.</summary>
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

    /// <summary>Gets or sets the execution order (lower runs first).</summary>
    public int Order { get; set; } = 100;

    /// <summary>Gets or sets the condition as JSON (<c>ConditionGroup</c>); empty matches every record.</summary>
    public string? ConditionJson { get; set; }

    /// <summary>Gets or sets the actions as JSON array.</summary>
    public string ActionsJson { get; set; } = "[]";
}
