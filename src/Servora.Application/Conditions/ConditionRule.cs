namespace Servora.Application.Conditions;

/// <summary>A single field comparison, for example <c>Priority Equals 1</c>.</summary>
public sealed class ConditionRule
{
    /// <summary>Gets or sets the field name (property name of the table).</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>Gets or sets the operator.</summary>
    public ConditionOperator Operator { get; set; } = ConditionOperator.Equals;

    /// <summary>
    /// Gets or sets the comparison value as invariant string. Enums use their numeric or name value, references use
    /// the record id, dates use ISO 8601, lists (<see cref="ConditionOperator.In"/>) are comma separated.
    /// </summary>
    public string? Value { get; set; }
}
