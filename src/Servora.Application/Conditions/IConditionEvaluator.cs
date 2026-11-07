using System.Linq.Expressions;

namespace Servora.Application.Conditions;

/// <summary>Evaluates <see cref="ConditionGroup"/>s in memory and translates them into LINQ predicates.</summary>
public interface IConditionEvaluator
{
    /// <summary>Evaluates a condition against record field values (in memory).</summary>
    /// <param name="condition">The condition; <see langword="null"/> or empty always matches.</param>
    /// <param name="record">The record values keyed by field name (case-insensitive).</param>
    /// <param name="context">Optional context for change-aware operators.</param>
    /// <returns><see langword="true"/> when the record matches.</returns>
    bool Evaluate(ConditionGroup? condition, IReadOnlyDictionary<string, object?> record, ConditionContext? context = null);

    /// <summary>
    /// Translates a condition into a predicate that EF Core can execute in the database. Change-aware operators are
    /// not supported in queries and are ignored.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="condition">The condition.</param>
    /// <param name="context">Optional context (for <see cref="ConditionOperator.IsMe"/>).</param>
    /// <returns>The predicate expression.</returns>
    Expression<Func<T, bool>> ToExpression<T>(ConditionGroup? condition, ConditionContext? context = null);
}
