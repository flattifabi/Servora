using System.Linq.Expressions;
using Servora.Application.Conditions;

namespace Servora.Infrastructure.Placeholders;

/// <summary>Placeholder <see cref="IConditionEvaluator"/>: only empty conditions match until the Platform module (T-07) is registered.</summary>
/// <remarks>Placeholder registered by the core kernel so every module can run in isolation. The owning module registers the real implementation later in the DI container, which takes precedence.</remarks>
public sealed class PassThroughConditionEvaluator : IConditionEvaluator
{
    /// <inheritdoc />
    public bool Evaluate(ConditionGroup? condition, IReadOnlyDictionary<string, object?> record, ConditionContext? context = null) =>
        condition is null || condition.IsEmpty;

    /// <inheritdoc />
    public Expression<Func<T, bool>> ToExpression<T>(ConditionGroup? condition, ConditionContext? context = null)
    {
        var matches = condition is null || condition.IsEmpty;
        return _ => matches;
    }
}
