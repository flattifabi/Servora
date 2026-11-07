using System.Globalization;
using Servora.Application.Automation.Model;

namespace Servora.Application.Automation.Engine;

/// <summary>Evaluates data pill conditions of "If" steps. Operands are rendered templates; no code is executed.</summary>
public static class FlowConditionEvaluator
{
    /// <summary>Evaluates a condition.</summary>
    /// <param name="condition">The condition; <see langword="null"/> or empty matches.</param>
    /// <param name="render">Renders a template to text.</param>
    /// <returns><see langword="true"/> when the condition matches.</returns>
    public static bool Evaluate(FlowCondition? condition, Func<string?, string> render)
    {
        if (condition is null || condition.Rules.Count == 0)
        {
            return true;
        }

        return condition.MatchAny
            ? condition.Rules.Any(r => EvaluateRule(r, render))
            : condition.Rules.All(r => EvaluateRule(r, render));
    }

    /// <summary>Evaluates one rule.</summary>
    /// <param name="rule">The rule.</param>
    /// <param name="render">Renders a template to text.</param>
    /// <returns><see langword="true"/> when the rule matches.</returns>
    public static bool EvaluateRule(FlowConditionRule rule, Func<string?, string> render)
    {
        var left = render(rule.Left) ?? string.Empty;
        var right = render(rule.Right) ?? string.Empty;
        return Compare(left, rule.Operator, right);
    }

    /// <summary>Compares two rendered operands.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="op">The operator.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The comparison result.</returns>
    public static bool Compare(string left, FlowConditionOperator op, string right)
    {
        switch (op)
        {
            case FlowConditionOperator.IsEmpty:
                return string.IsNullOrWhiteSpace(left);
            case FlowConditionOperator.IsNotEmpty:
                return !string.IsNullOrWhiteSpace(left);
            case FlowConditionOperator.Contains:
                return left.Contains(right, StringComparison.OrdinalIgnoreCase);
            case FlowConditionOperator.NotContains:
                return !left.Contains(right, StringComparison.OrdinalIgnoreCase);
            case FlowConditionOperator.StartsWith:
                return left.StartsWith(right, StringComparison.OrdinalIgnoreCase);
            case FlowConditionOperator.EndsWith:
                return left.EndsWith(right, StringComparison.OrdinalIgnoreCase);
            case FlowConditionOperator.In:
                return SplitList(right).Any(v => CompareValues(left, v) == 0);
            case FlowConditionOperator.NotIn:
                return !SplitList(right).Any(v => CompareValues(left, v) == 0);
            case FlowConditionOperator.Equals:
                return CompareValues(left, right) == 0;
            case FlowConditionOperator.NotEquals:
                return CompareValues(left, right) != 0;
            case FlowConditionOperator.GreaterThan:
                return CompareValues(left, right) > 0;
            case FlowConditionOperator.GreaterOrEqual:
                return CompareValues(left, right) >= 0;
            case FlowConditionOperator.LessThan:
                return CompareValues(left, right) < 0;
            case FlowConditionOperator.LessOrEqual:
                return CompareValues(left, right) <= 0;
            default:
                return false;
        }
    }

    private static int CompareValues(string left, string right)
    {
        left = left.Trim();
        right = right.Trim();
        if (decimal.TryParse(left, NumberStyles.Float, CultureInfo.InvariantCulture, out var l)
            && decimal.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out var r))
        {
            return l.CompareTo(r);
        }

        if (left.Length >= 8 && right.Length >= 8
            && DateTimeOffset.TryParse(left, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var ld)
            && DateTimeOffset.TryParse(right, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var rd))
        {
            return ld.CompareTo(rd);
        }

        if (bool.TryParse(left, out var lb) && bool.TryParse(right, out var rb))
        {
            return lb.CompareTo(rb);
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> SplitList(string value) =>
        value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
