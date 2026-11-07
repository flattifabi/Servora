using Servora.Application.Conditions;

namespace Servora.Application.Analytics;

/// <summary>
/// Detects at runtime whether the registered <see cref="IConditionEvaluator"/> can evaluate rules. The kernel placeholder
/// (used until the Platform engine is installed) only matches empty conditions; reports then run unfiltered and say so
/// instead of silently returning nothing.
/// </summary>
public static class ConditionSupport
{
    private static readonly ConditionGroup s_probe = new() { Rules = [new ConditionRule { Field = "Probe", Operator = ConditionOperator.Equals, Value = "servora" }] };
    private static readonly Dictionary<string, object?> s_record = new(StringComparer.OrdinalIgnoreCase) { ["Probe"] = "servora" };

    /// <summary>Checks whether the evaluator evaluates real rules.</summary>
    /// <param name="evaluator">The evaluator.</param>
    /// <returns><see langword="true"/> when filters are supported.</returns>
    public static bool IsSupported(IConditionEvaluator evaluator)
    {
        try
        {
            return evaluator.Evaluate(s_probe, s_record);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }
}
