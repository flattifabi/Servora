using System.Globalization;
using System.Linq.Expressions;
using Servora.Application.Conditions;

namespace Servora.Tests.Unit.Automation.Fakes;

/// <summary>Minimal <see cref="IConditionEvaluator"/> supporting AND / OR over a few operators.</summary>
internal sealed class SimpleConditionEvaluator : IConditionEvaluator
{
    /// <inheritdoc />
    public bool Evaluate(ConditionGroup? condition, IReadOnlyDictionary<string, object?> record, ConditionContext? context = null)
    {
        if (condition is null || condition.IsEmpty)
        {
            return true;
        }

        var results = condition.Rules.Select(rule => Evaluate(rule, record)).Concat(condition.Groups.Select(g => Evaluate(g, record, context)));
        return condition.Logic == ConditionLogic.Or ? results.Any(r => r) : results.All(r => r);
    }

    /// <inheritdoc />
    public Expression<Func<T, bool>> ToExpression<T>(ConditionGroup? condition, ConditionContext? context = null) => _ => true;

    private static bool Evaluate(ConditionRule rule, IReadOnlyDictionary<string, object?> record)
    {
        var actual = record.FirstOrDefault(p => string.Equals(p.Key, rule.Field, StringComparison.OrdinalIgnoreCase)).Value;
        var text = Convert.ToString(actual, CultureInfo.InvariantCulture) ?? string.Empty;
        return rule.Operator switch
        {
            ConditionOperator.Equals => string.Equals(text, rule.Value, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.NotEquals => !string.Equals(text, rule.Value, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.LessOrEqual => double.TryParse(text, CultureInfo.InvariantCulture, out var a)
                && double.TryParse(rule.Value, CultureInfo.InvariantCulture, out var b) && a <= b,
            ConditionOperator.IsNotEmpty => text.Length > 0,
            _ => false,
        };
    }
}
