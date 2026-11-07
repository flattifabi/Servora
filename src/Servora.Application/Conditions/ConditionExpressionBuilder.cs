using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Servora.Application.Tables;
using Servora.Domain.Common;
using Servora.Domain.Platform;

namespace Servora.Application.Conditions;

/// <summary>
/// Translates <see cref="ConditionGroup"/>s into provider-neutral LINQ predicates that EF Core executes in SQL on
/// SQLite, SQL Server and PostgreSQL. Text comparisons are case-insensitive (<c>LOWER()</c>), condition values become
/// SQL parameters, unknown or <see cref="NotExposedAttribute"/> fields never match and change-aware operators are ignored.
/// </summary>
internal static class ConditionExpressionBuilder
{
    /// <summary>The maximum nesting depth of condition groups.</summary>
    public const int MaxDepth = 10;

    private static readonly ConcurrentDictionary<(Type Type, string Name), PropertyInfo?> s_properties = new();
    private static readonly MethodInfo s_toLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
    private static readonly MethodInfo s_trim = typeof(string).GetMethod(nameof(string.Trim), Type.EmptyTypes)!;
    private static readonly MethodInfo s_contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
    private static readonly MethodInfo s_startsWith = typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;
    private static readonly MethodInfo s_endsWith = typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!;
    private static readonly MethodInfo s_compare = typeof(string).GetMethod(nameof(string.Compare), [typeof(string), typeof(string)])!;
    private static readonly MethodInfo s_queryableAny = typeof(Queryable).GetMethods()
        .Single(m => m.Name == nameof(Queryable.Any) && m.GetParameters().Length == 2)
        .MakeGenericMethod(typeof(RecordFieldValue));

    /// <summary>Builds the predicate.</summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="condition">The condition.</param>
    /// <param name="context">The context (current user for <see cref="ConditionOperator.IsMe"/>).</param>
    /// <param name="options">Options for indexed (custom) fields.</param>
    /// <returns>The predicate.</returns>
    public static Expression<Func<T, bool>> Build<T>(ConditionGroup? condition, ConditionContext? context, ConditionQueryOptions? options)
    {
        var parameter = Expression.Parameter(typeof(T), "r");
        var body = condition is null || condition.IsEmpty ? null : BuildGroup(condition, parameter, context, options, 0);
        return Expression.Lambda<Func<T, bool>>(body ?? Expression.Constant(true), parameter);
    }

    /// <summary>Finds a filterable property (public, readable, scalar, not <see cref="NotExposedAttribute"/>) by name, case-insensitively.</summary>
    /// <param name="type">The entity type.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The property or <see langword="null"/>.</returns>
    public static PropertyInfo? FindProperty(Type type, string name) =>
        s_properties.GetOrAdd((type, name), key =>
        {
            var property = key.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => string.Equals(p.Name, key.Name, StringComparison.OrdinalIgnoreCase));
            if (property is null || property.GetMethod is null || property.GetIndexParameters().Length > 0
                || property.GetCustomAttribute<NotExposedAttribute>() is not null || !IsScalar(property.PropertyType))
            {
                return null;
            }

            return property;
        });

    private static bool IsScalar(Type type)
    {
        var u = Nullable.GetUnderlyingType(type) ?? type;
        return u.IsPrimitive || u.IsEnum || u == typeof(string) || u == typeof(decimal) || u == typeof(Guid)
            || u == typeof(DateTimeOffset) || u == typeof(DateTime) || u == typeof(DateOnly);
    }

    private static Expression? BuildGroup(ConditionGroup group, ParameterExpression parameter, ConditionContext? context, ConditionQueryOptions? options, int depth)
    {
        if (depth > MaxDepth)
        {
            return Expression.Constant(false);
        }

        var parts = new List<Expression>();
        foreach (var rule in group.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Field))
            {
                continue;
            }

            var expression = BuildRule(rule, parameter, context, options);
            if (expression is not null)
            {
                parts.Add(expression);
            }
        }

        foreach (var nested in group.Groups)
        {
            if (nested.IsEmpty)
            {
                continue;
            }

            var expression = BuildGroup(nested, parameter, context, options, depth + 1);
            if (expression is not null)
            {
                parts.Add(expression);
            }
        }

        if (parts.Count == 0)
        {
            return null;
        }

        return parts.Aggregate((left, right) => group.Logic == ConditionLogic.Or ? Expression.OrElse(left, right) : Expression.AndAlso(left, right));
    }

    private static Expression? BuildRule(ConditionRule rule, ParameterExpression parameter, ConditionContext? context, ConditionQueryOptions? options)
    {
        if (rule.Operator is ConditionOperator.Changed or ConditionOperator.ChangedTo or ConditionOperator.ChangedFrom)
        {
            return null;
        }

        var property = FindProperty(parameter.Type, rule.Field);
        if (property is null)
        {
            var field = options?.ResolveIndexedField(rule.Field);
            return field is not null && options is not null && FindProperty(parameter.Type, nameof(Entity.Id)) is not null
                ? BuildIndexed(rule, field, parameter, context, options)
                : False;
        }

        return BuildMember(rule, Expression.Property(parameter, property), context);
    }

    private static ConstantExpression False => Expression.Constant(false);

    private static ConstantExpression True => Expression.Constant(true);

    private static Expression BuildMember(ConditionRule rule, MemberExpression member, ConditionContext? context)
    {
        var type = member.Type;
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        switch (rule.Operator)
        {
            case ConditionOperator.IsEmpty:
                return EmptyCheck(member);
            case ConditionOperator.IsNotEmpty:
                return Expression.Not(EmptyCheck(member));
            case ConditionOperator.IsMe:
                return underlying == typeof(Guid) && context?.CurrentUserId is { } userId
                    ? Expression.Equal(member, Parameter(userId, type))
                    : False;
            case ConditionOperator.Equals:
                return EqualsCheck(member, rule.Value, negate: false);
            case ConditionOperator.NotEquals:
                return EqualsCheck(member, rule.Value, negate: true);
            case ConditionOperator.Contains or ConditionOperator.NotContains or ConditionOperator.StartsWith or ConditionOperator.EndsWith:
                return TextCheck(member, rule.Operator, rule.Value ?? string.Empty);
            case ConditionOperator.GreaterThan or ConditionOperator.GreaterOrEqual or ConditionOperator.LessThan or ConditionOperator.LessOrEqual:
                return CompareCheck(member, rule.Operator, rule.Value);
            case ConditionOperator.In:
                return InCheck(member, rule.Value, negate: false);
            case ConditionOperator.NotIn:
                return InCheck(member, rule.Value, negate: true);
            default:
                return False;
        }
    }

    private static Expression EmptyCheck(Expression member)
    {
        var type = member.Type;
        if (type == typeof(string))
        {
            return Expression.OrElse(
                Expression.Equal(member, Expression.Constant(null, typeof(string))),
                Expression.Equal(Expression.Call(member, s_trim), Expression.Constant(string.Empty)));
        }

        if (type == typeof(Guid))
        {
            return Expression.Equal(member, Expression.Constant(Guid.Empty));
        }

        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            var isNull = Expression.Equal(member, Expression.Constant(null, type));
            return underlying == typeof(Guid)
                ? Expression.OrElse(isNull, Expression.Equal(member, Expression.Constant(Guid.Empty, type)))
                : isNull;
        }

        return False;
    }

    private static Expression NotNull(Expression member) => Expression.NotEqual(member, Expression.Constant(null, member.Type));

    private static Expression Lower(Expression member) => Expression.Call(member, s_toLower);

    private static Expression EqualsCheck(MemberExpression member, string? value, bool negate)
    {
        if (string.IsNullOrEmpty(value))
        {
            return negate ? Expression.Not(EmptyCheck(member)) : EmptyCheck(member);
        }

        var type = member.Type;
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string))
        {
            var lowered = Parameter(value.ToLowerInvariant(), typeof(string));
            return negate
                ? Expression.OrElse(Expression.Equal(member, Expression.Constant(null, typeof(string))), Expression.NotEqual(Lower(member), lowered))
                : Expression.AndAlso(NotNull(member), Expression.Equal(Lower(member), lowered));
        }

        if ((underlying == typeof(DateTimeOffset) || underlying == typeof(DateTime))
            && ConditionValueConverter.TryParseDate(value, out var date, out var dateOnly) && dateOnly)
        {
            object from = underlying == typeof(DateTimeOffset) ? date : date.UtcDateTime;
            object to = underlying == typeof(DateTimeOffset) ? date.AddDays(1) : date.AddDays(1).UtcDateTime;
            var range = Expression.AndAlso(
                Expression.GreaterThanOrEqual(member, Parameter(from, type)),
                Expression.LessThan(member, Parameter(to, type)));
            return negate ? Expression.Not(range) : range;
        }

        if (!ConditionValueConverter.TryConvert(value, underlying, out var converted))
        {
            return negate ? True : False;
        }

        var (left, right) = Operands(member, converted!);
        return negate ? Expression.NotEqual(left, right) : Expression.Equal(left, right);
    }

    private static (Expression Left, Expression Right) Operands(Expression member, object value)
    {
        var type = member.Type;
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (!underlying.IsEnum)
        {
            return (member, Parameter(value, type));
        }

        // Expression trees do not define comparison operators for enums: compare the underlying numeric values.
        var numeric = Enum.GetUnderlyingType(underlying);
        var numericType = type == underlying ? numeric : typeof(Nullable<>).MakeGenericType(numeric);
        var numericValue = Convert.ChangeType(value, numeric, System.Globalization.CultureInfo.InvariantCulture);
        return (Expression.Convert(member, numericType), Parameter(numericValue, numericType));
    }

    private static Expression TextCheck(MemberExpression member, ConditionOperator op, string value)
    {
        if (member.Type != typeof(string))
        {
            return op == ConditionOperator.NotContains ? True : False;
        }

        if (value.Length == 0)
        {
            return op == ConditionOperator.NotContains ? Expression.Equal(member, Expression.Constant(null, typeof(string))) : NotNull(member);
        }

        var lowered = Parameter(value.ToLowerInvariant(), typeof(string));
        var method = op switch
        {
            ConditionOperator.StartsWith => s_startsWith,
            ConditionOperator.EndsWith => s_endsWith,
            _ => s_contains,
        };
        var call = Expression.Call(Lower(member), method, lowered);
        return op == ConditionOperator.NotContains
            ? Expression.OrElse(Expression.Equal(member, Expression.Constant(null, typeof(string))), Expression.Not(call))
            : Expression.AndAlso(NotNull(member), call);
    }

    private static Expression CompareCheck(MemberExpression member, ConditionOperator op, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return False;
        }

        var type = member.Type;
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string))
        {
            var compare = Expression.Call(s_compare, member, Parameter(value, typeof(string)));
            return Expression.AndAlso(NotNull(member), Comparison(op, compare, Expression.Constant(0)));
        }

        if (underlying == typeof(bool) || underlying == typeof(Guid) || !ConditionValueConverter.TryConvert(value, underlying, out var converted))
        {
            return False;
        }

        var (left, right) = Operands(member, converted!);
        return Comparison(op, left, right);
    }

    private static Expression Comparison(ConditionOperator op, Expression left, Expression right) => op switch
    {
        ConditionOperator.GreaterThan => Expression.GreaterThan(left, right),
        ConditionOperator.GreaterOrEqual => Expression.GreaterThanOrEqual(left, right),
        ConditionOperator.LessThan => Expression.LessThan(left, right),
        _ => Expression.LessThanOrEqual(left, right),
    };

    private static Expression InCheck(MemberExpression member, string? value, bool negate)
    {
        var items = ConditionValueConverter.SplitList(value);
        var type = member.Type;
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        Expression contains;
        if (type == typeof(string))
        {
            var lowered = items.Select(i => i.ToLowerInvariant()).Distinct().ToList();
            if (lowered.Count == 0)
            {
                return negate ? True : False;
            }

            contains = Expression.AndAlso(NotNull(member), ListContains(lowered, typeof(string), Lower(member)));
            return negate ? Expression.Not(contains) : contains;
        }

        Expression target = member;
        var elementType = type;
        Type? numeric = null;
        if (underlying.IsEnum)
        {
            numeric = Enum.GetUnderlyingType(underlying);
            elementType = type == underlying ? numeric : typeof(Nullable<>).MakeGenericType(numeric);
            target = Expression.Convert(member, elementType);
        }

        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        foreach (var item in items)
        {
            if (ConditionValueConverter.TryConvert(item, underlying, out var converted))
            {
                list.Add(numeric is null ? converted : Convert.ChangeType(converted, numeric, System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        if (list.Count == 0)
        {
            return negate ? True : False;
        }

        contains = ListContains(list, elementType, target);
        return negate ? Expression.Not(contains) : contains;
    }

    private static MethodCallExpression ListContains(object list, Type elementType, Expression target)
    {
        var listType = typeof(List<>).MakeGenericType(elementType);
        var method = typeof(Enumerable).GetMethods()
            .Single(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
            .MakeGenericMethod(elementType);
        return Expression.Call(method, Parameter(list, listType), target);
    }

    private static Expression Parameter(object value, Type type)
    {
        var holder = Activator.CreateInstance(typeof(ConditionParameter<>).MakeGenericType(type), value)!;
        return Expression.Property(Expression.Constant(holder), nameof(ConditionParameter<object>.Value));
    }

    private static Expression BuildIndexed(ConditionRule rule, FieldDefinition field, ParameterExpression parameter, ConditionContext? context, ConditionQueryOptions options)
    {
        var row = Expression.Parameter(typeof(RecordFieldValue), "v");
        var recordId = Expression.Property(parameter, nameof(Entity.Id));
        Expression scope = Expression.AndAlso(
            Expression.AndAlso(
                Expression.Equal(Expression.Property(row, nameof(RecordFieldValue.TableName)), Parameter(options.TableName, typeof(string))),
                Expression.Equal(Expression.Property(row, nameof(RecordFieldValue.RecordId)), recordId)),
            Expression.Equal(Expression.Property(row, nameof(RecordFieldValue.Field)), Parameter(field.Name, typeof(string))));

        Expression Exists(Expression? predicate)
        {
            var body = predicate is null ? scope : Expression.AndAlso(scope, predicate);
            var lambda = Expression.Lambda<Func<RecordFieldValue, bool>>(body, row);
            return Expression.Call(s_queryableAny, options.FieldValues.Expression, Expression.Quote(lambda));
        }

        var value = rule.Value;
        switch (rule.Operator)
        {
            case ConditionOperator.IsEmpty:
                return Expression.Not(Exists(null));
            case ConditionOperator.IsNotEmpty:
                return Exists(null);
            case ConditionOperator.Equals or ConditionOperator.NotEquals when string.IsNullOrEmpty(value):
                return rule.Operator == ConditionOperator.Equals ? Expression.Not(Exists(null)) : Exists(null);
            case ConditionOperator.IsMe:
                if (field.Type != FieldType.Reference || context?.CurrentUserId is not { } userId)
                {
                    return False;
                }

                return Exists(Expression.Equal(Expression.Property(row, nameof(RecordFieldValue.TextValue)), Parameter(userId.ToString("D"), typeof(string))));
        }

        var predicate = IndexedPredicate(rule.Operator, field, value ?? string.Empty, row);
        var negated = rule.Operator is ConditionOperator.NotEquals or ConditionOperator.NotContains or ConditionOperator.NotIn;
        if (predicate is null)
        {
            return negated ? True : False;
        }

        return negated ? Expression.Not(Exists(predicate)) : Exists(predicate);
    }

    private static Expression? IndexedPredicate(ConditionOperator op, FieldDefinition field, string value, ParameterExpression row)
    {
        var column = FieldIndexNormalizer.ColumnFor(field.Type);
        var text = Expression.Property(row, nameof(RecordFieldValue.TextValue));
        var number = Expression.Property(row, nameof(RecordFieldValue.NumberValue));
        var date = Expression.Property(row, nameof(RecordFieldValue.DateValue));

        switch (op)
        {
            case ConditionOperator.Equals or ConditionOperator.NotEquals:
                return column switch
                {
                    FieldIndexColumn.Number => ConditionValueConverter.TryConvert(value, typeof(double), out var n) ? Expression.Equal(number, Parameter(n!, typeof(double?))) : null,
                    FieldIndexColumn.Date => DateEquals(date, value),
                    _ => FieldIndexNormalizer.NormalizeConditionText(field.Type, value) is { } t ? Expression.Equal(text, Parameter(t, typeof(string))) : null,
                };
            case ConditionOperator.Contains or ConditionOperator.NotContains or ConditionOperator.StartsWith or ConditionOperator.EndsWith:
                if (column != FieldIndexColumn.Text)
                {
                    return null;
                }

                var method = op switch
                {
                    ConditionOperator.StartsWith => s_startsWith,
                    ConditionOperator.EndsWith => s_endsWith,
                    _ => s_contains,
                };
                return Expression.AndAlso(NotNull(text), Expression.Call(text, method, Parameter(value.ToLowerInvariant(), typeof(string))));
            case ConditionOperator.GreaterThan or ConditionOperator.GreaterOrEqual or ConditionOperator.LessThan or ConditionOperator.LessOrEqual:
                return column switch
                {
                    FieldIndexColumn.Number => ConditionValueConverter.TryConvert(value, typeof(double), out var n) ? Comparison(op, number, Parameter(n!, typeof(double?))) : null,
                    FieldIndexColumn.Date => ConditionValueConverter.TryParseDate(value, out var d, out _) ? Comparison(op, date, Parameter(d, typeof(DateTimeOffset?))) : null,
                    _ => field.Type is FieldType.Boolean or FieldType.Guid or FieldType.Reference
                        ? null
                        : Expression.AndAlso(NotNull(text), Comparison(op, Expression.Call(s_compare, text, Parameter(FieldIndexNormalizer.NormalizeText(value), typeof(string))), Expression.Constant(0))),
                };
            case ConditionOperator.In or ConditionOperator.NotIn:
                var items = ConditionValueConverter.SplitList(value);
                if (column == FieldIndexColumn.Number)
                {
                    var numbers = items.Select(i => ConditionValueConverter.TryConvert(i, typeof(double), out var n) ? (double?)n : null).Where(n => n is not null).ToList();
                    return numbers.Count == 0 ? null : ListContains(numbers, typeof(double?), number);
                }

                if (column == FieldIndexColumn.Date)
                {
                    var dates = items.Select(i => ConditionValueConverter.TryParseDate(i, out var d, out _) ? (DateTimeOffset?)d : null).Where(d => d is not null).ToList();
                    return dates.Count == 0 ? null : ListContains(dates, typeof(DateTimeOffset?), date);
                }

                var texts = items.Select(i => FieldIndexNormalizer.NormalizeConditionText(field.Type, i)).Where(t => t is not null).Cast<string>().Distinct().ToList();
                return texts.Count == 0 ? null : ListContains(texts, typeof(string), text);
            default:
                return null;
        }
    }

    private static Expression? DateEquals(MemberExpression date, string value)
    {
        if (!ConditionValueConverter.TryParseDate(value, out var parsed, out var dateOnly))
        {
            return null;
        }

        if (!dateOnly)
        {
            return Expression.Equal(date, Parameter(parsed, typeof(DateTimeOffset?)));
        }

        return Expression.AndAlso(
            Expression.GreaterThanOrEqual(date, Parameter(parsed, typeof(DateTimeOffset?))),
            Expression.LessThan(date, Parameter(parsed.AddDays(1), typeof(DateTimeOffset?))));
    }
}
