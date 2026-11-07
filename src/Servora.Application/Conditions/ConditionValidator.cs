using Servora.Application.Tables;

namespace Servora.Application.Conditions;

/// <summary>Validates conditions against a table definition (known fields only, bounded size and depth).</summary>
public static class ConditionValidator
{
    /// <summary>The maximum number of rules in a condition (all levels).</summary>
    public const int MaxRules = 100;

    /// <summary>The maximum length of a rule value.</summary>
    public const int MaxValueLength = 4000;

    /// <summary>Validates a condition.</summary>
    /// <param name="table">The table the condition applies to.</param>
    /// <param name="condition">The condition (null is valid).</param>
    /// <param name="allowChangeOperators">Whether change-aware operators are allowed.</param>
    /// <returns>A user-safe error message or <see langword="null"/> when valid.</returns>
    public static string? Validate(TableDefinition table, ConditionGroup? condition, bool allowChangeOperators = true)
    {
        if (condition is null)
        {
            return null;
        }

        var count = 0;
        return ValidateGroup(table, condition, allowChangeOperators, 0, ref count);
    }

    private static string? ValidateGroup(TableDefinition table, ConditionGroup group, bool allowChangeOperators, int depth, ref int count)
    {
        if (depth > ConditionExpressionBuilder.MaxDepth)
        {
            return $"Conditions can be nested at most {ConditionExpressionBuilder.MaxDepth} levels deep.";
        }

        foreach (var rule in group.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Field))
            {
                continue;
            }

            if (++count > MaxRules)
            {
                return $"A condition can contain at most {MaxRules} rules.";
            }

            if (!Enum.IsDefined(rule.Operator))
            {
                return "The condition contains an unknown operator.";
            }

            if (table.FindField(rule.Field) is null)
            {
                return $"Unknown field '{rule.Field}' in condition.";
            }

            if (!allowChangeOperators && rule.Operator is ConditionOperator.Changed or ConditionOperator.ChangedTo or ConditionOperator.ChangedFrom)
            {
                return "Change operators (changes, changes to, changes from) are not supported here.";
            }

            if (rule.Value is { Length: > MaxValueLength })
            {
                return $"Condition values must not exceed {MaxValueLength} characters.";
            }
        }

        foreach (var nested in group.Groups)
        {
            if (ValidateGroup(table, nested, allowChangeOperators, depth + 1, ref count) is { } error)
            {
                return error;
            }
        }

        return null;
    }
}
