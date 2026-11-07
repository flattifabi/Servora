using System.Linq.Expressions;
using Servora.Application.Events;

namespace Servora.Application.Conditions;

/// <summary>
/// Full <see cref="IConditionEvaluator"/>: evaluates conditions in memory (business rules, SLAs, notifications) and
/// translates them into provider-neutral EF Core predicates (lists, Table API, reports).
/// </summary>
/// <remarks>
/// Semantics shared by both paths: text comparisons are case-insensitive; condition values are coerced to the field
/// type (enums by value or name, identifiers, ISO dates, invariant numbers, booleans such as <c>true/1/yes</c>); a
/// date-only value compared with <see cref="ConditionOperator.Equals"/> matches the whole UTC day; empty means null,
/// blank text or <see cref="Guid.Empty"/>; rules without a field are skipped; groups nest up to
/// <see cref="ConditionExpressionBuilder.MaxDepth"/> levels.
/// </remarks>
public sealed class ConditionEvaluator : IConditionEvaluator
{
    /// <inheritdoc />
    public bool Evaluate(ConditionGroup? condition, IReadOnlyDictionary<string, object?> record, ConditionContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (condition is null || condition.IsEmpty)
        {
            return true;
        }

        var values = record is Dictionary<string, object?> dictionary && ReferenceEquals(dictionary.Comparer, StringComparer.OrdinalIgnoreCase)
            ? record
            : CopyCaseInsensitive(record);
        return EvaluateGroup(condition, values, context, 0) ?? true;
    }

    /// <inheritdoc />
    public Expression<Func<T, bool>> ToExpression<T>(ConditionGroup? condition, ConditionContext? context = null) =>
        ConditionExpressionBuilder.Build<T>(condition, context, null);

    /// <summary>Translates a condition into a predicate, matching custom (non-property) fields through the field value index.</summary>
    /// <typeparam name="T">The entity type (must have an <c>Id</c> property for indexed fields).</typeparam>
    /// <param name="condition">The condition.</param>
    /// <param name="context">The context.</param>
    /// <param name="options">The index options.</param>
    /// <returns>The predicate expression.</returns>
    public Expression<Func<T, bool>> ToExpression<T>(ConditionGroup? condition, ConditionContext? context, ConditionQueryOptions? options) =>
        ConditionExpressionBuilder.Build<T>(condition, context, options);

    private static Dictionary<string, object?> CopyCaseInsensitive(IReadOnlyDictionary<string, object?> record)
    {
        var copy = new Dictionary<string, object?>(record.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in record)
        {
            copy[key] = value;
        }

        return copy;
    }

    /// <summary>Evaluates a group; returns <see langword="null"/> when the group contains no effective rule.</summary>
    private static bool? EvaluateGroup(ConditionGroup group, IReadOnlyDictionary<string, object?> values, ConditionContext? context, int depth)
    {
        if (depth > ConditionExpressionBuilder.MaxDepth)
        {
            return false;
        }

        var any = false;
        foreach (var rule in group.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Field))
            {
                continue;
            }

            any = true;
            var matches = EvaluateRule(rule, values, context);
            if (group.Logic == ConditionLogic.Or && matches)
            {
                return true;
            }

            if (group.Logic == ConditionLogic.And && !matches)
            {
                return false;
            }
        }

        foreach (var nested in group.Groups)
        {
            if (nested.IsEmpty || EvaluateGroup(nested, values, context, depth + 1) is not { } matches)
            {
                continue;
            }

            any = true;
            if (group.Logic == ConditionLogic.Or && matches)
            {
                return true;
            }

            if (group.Logic == ConditionLogic.And && !matches)
            {
                return false;
            }
        }

        if (!any)
        {
            return null;
        }

        return group.Logic == ConditionLogic.And;
    }

    private static bool EvaluateRule(ConditionRule rule, IReadOnlyDictionary<string, object?> values, ConditionContext? context)
    {
        values.TryGetValue(rule.Field, out var actual);
        actual = ConditionValueConverter.Normalize(actual);
        switch (rule.Operator)
        {
            case ConditionOperator.Changed:
                return FindChange(rule.Field, context) is not null;
            case ConditionOperator.ChangedTo:
                return FindChange(rule.Field, context) is { } changedTo && ConditionValueConverter.ValuesEqual(changedTo.NewValue, rule.Value);
            case ConditionOperator.ChangedFrom:
                return FindChange(rule.Field, context) is { } changedFrom && ConditionValueConverter.ValuesEqual(changedFrom.OldValue, rule.Value);
            case ConditionOperator.IsMe:
                return context?.CurrentUserId is { } userId && ConditionValueConverter.TryGetGuid(actual, out var id) && id == userId;
            case ConditionOperator.IsEmpty:
                return ConditionValueConverter.IsEmpty(actual);
            case ConditionOperator.IsNotEmpty:
                return !ConditionValueConverter.IsEmpty(actual);
            case ConditionOperator.Equals:
                return ConditionValueConverter.ValuesEqual(actual, rule.Value);
            case ConditionOperator.NotEquals:
                return !ConditionValueConverter.ValuesEqual(actual, rule.Value);
            case ConditionOperator.Contains or ConditionOperator.NotContains or ConditionOperator.StartsWith or ConditionOperator.EndsWith:
                return EvaluateText(rule.Operator, actual, rule.Value ?? string.Empty);
            case ConditionOperator.GreaterThan:
                return ConditionValueConverter.Compare(actual, rule.Value) is > 0;
            case ConditionOperator.GreaterOrEqual:
                return ConditionValueConverter.Compare(actual, rule.Value) is >= 0;
            case ConditionOperator.LessThan:
                return ConditionValueConverter.Compare(actual, rule.Value) is < 0;
            case ConditionOperator.LessOrEqual:
                return ConditionValueConverter.Compare(actual, rule.Value) is <= 0;
            case ConditionOperator.In:
                return ConditionValueConverter.SplitList(rule.Value).Any(item => ConditionValueConverter.ValuesEqual(actual, item));
            case ConditionOperator.NotIn:
                return !ConditionValueConverter.SplitList(rule.Value).Any(item => ConditionValueConverter.ValuesEqual(actual, item));
            default:
                return false;
        }
    }

    private static bool EvaluateText(ConditionOperator op, object? actual, string needle)
    {
        // Text operators only apply to text values; this mirrors the database translation.
        if (actual is not string text)
        {
            return op == ConditionOperator.NotContains;
        }

        return op switch
        {
            ConditionOperator.Contains => text.Contains(needle, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.NotContains => !text.Contains(needle, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.StartsWith => text.StartsWith(needle, StringComparison.OrdinalIgnoreCase),
            _ => text.EndsWith(needle, StringComparison.OrdinalIgnoreCase),
        };
    }

    private static RecordFieldChange? FindChange(string field, ConditionContext? context) =>
        context?.Changes?.FirstOrDefault(c => string.Equals(c.Field, field, StringComparison.OrdinalIgnoreCase));
}
