namespace Servora.Application.BusinessRules;

/// <summary>One action of a business rule (stored as JSON).</summary>
public sealed class BusinessRuleAction
{
    /// <summary>Gets or sets the action type.</summary>
    public BusinessRuleActionType Type { get; set; }

    /// <summary>Gets or sets the target field of <see cref="BusinessRuleActionType.SetField"/>.</summary>
    public string? Field { get; set; }

    /// <summary>Gets or sets the value of <see cref="BusinessRuleActionType.SetField"/> (invariant string).</summary>
    public string? Value { get; set; }

    /// <summary>Gets or sets the abort message or work note text (supports <c>${Field}</c> placeholders).</summary>
    public string? Message { get; set; }

    /// <summary>Gets or sets the flow or notification of <see cref="BusinessRuleActionType.StartFlow"/> / <see cref="BusinessRuleActionType.SendNotification"/>.</summary>
    public Guid? TargetId { get; set; }
}
