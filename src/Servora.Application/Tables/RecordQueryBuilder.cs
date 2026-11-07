using System.Linq.Expressions;
using System.Reflection;
using Servora.Application.Conditions;
using Servora.Domain.Common;
using Servora.Domain.Platform;

namespace Servora.Application.Tables;

/// <summary>Builds provider-neutral search and sort expressions for generic record queries.</summary>
internal static class RecordQueryBuilder
{
    private const int MaxSearchFields = 12;

    private static readonly MethodInfo s_where = QueryableMethod(nameof(Queryable.Where), 2);
    private static readonly MethodInfo s_select = QueryableMethod(nameof(Queryable.Select), 2);
    private static readonly MethodInfo s_firstOrDefault = typeof(Queryable).GetMethods()
        .Single(m => m.Name == nameof(Queryable.FirstOrDefault) && m.GetParameters().Length == 1);
    private static readonly MethodInfo s_orderBy = QueryableMethod(nameof(Queryable.OrderBy), 2);
    private static readonly MethodInfo s_orderByDescending = QueryableMethod(nameof(Queryable.OrderByDescending), 2);

    /// <summary>Applies a free-text search over the text fields of a table (typed properties and indexed custom fields).</summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="table">The table.</param>
    /// <param name="term">The search term.</param>
    /// <param name="options">The index options.</param>
    /// <returns>The filtered query.</returns>
    public static IQueryable<T> ApplySearch<T>(IQueryable<T> query, TableDefinition table, string term, ConditionQueryOptions options)
    {
        var rules = table.Fields
            .Where(f => f.Type is FieldType.String or FieldType.Text or FieldType.Email or FieldType.Url)
            .Where(f => f.IsCustom || ConditionExpressionBuilder.FindProperty(typeof(T), f.Name)?.PropertyType == typeof(string))
            .OrderByDescending(f => string.Equals(f.Name, table.DisplayField, StringComparison.OrdinalIgnoreCase))
            .ThenBy(f => f.Type == FieldType.Text)
            .Take(MaxSearchFields)
            .Select(f => new ConditionRule { Field = f.Name, Operator = ConditionOperator.Contains, Value = term })
            .ToList();
        if (rules.Count == 0)
        {
            return query.Where(_ => false);
        }

        var group = new ConditionGroup { Logic = ConditionLogic.Or, Rules = rules };
        return query.Where(ConditionExpressionBuilder.Build<T>(group, null, options));
    }

    /// <summary>Applies sorting by a typed property or an indexed custom field, with the record id as tie breaker.</summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="table">The table.</param>
    /// <param name="sortBy">The validated sort field, or null for the default order.</param>
    /// <param name="descending">Whether to sort descending.</param>
    /// <param name="index">The field value index set.</param>
    /// <returns>The ordered query.</returns>
    public static IQueryable<T> ApplySort<T>(IQueryable<T> query, TableDefinition table, string? sortBy, bool descending, IQueryable<RecordFieldValue> index)
        where T : Entity
    {
        var parameter = Expression.Parameter(typeof(T), "r");
        LambdaExpression? key = null;
        var field = sortBy is null ? null : table.FindField(sortBy);
        if (field is { IsCustom: true })
        {
            key = IndexedSortKey(parameter, table.Name, field, index);
        }
        else if (field is not null && ConditionExpressionBuilder.FindProperty(typeof(T), field.Name) is { } property
                 && (Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType) != typeof(decimal))
        {
            key = Expression.Lambda(Expression.Property(parameter, property), parameter);
        }

        if (key is null)
        {
            // Default: newest first when the entity is auditable.
            var created = ConditionExpressionBuilder.FindProperty(typeof(T), nameof(AuditableEntity.CreatedAt));
            if (created is not null)
            {
                key = Expression.Lambda(Expression.Property(parameter, created), parameter);
                descending = sortBy is null || descending;
            }
        }

        IOrderedQueryable<T> ordered;
        if (key is not null)
        {
            var method = (descending ? s_orderByDescending : s_orderBy).MakeGenericMethod(typeof(T), key.ReturnType);
            ordered = (IOrderedQueryable<T>)query.Provider.CreateQuery<T>(Expression.Call(method, query.Expression, Expression.Quote(key)));
            return ordered.ThenBy(e => e.Id);
        }

        return query.OrderBy(e => e.Id);
    }

    private static LambdaExpression IndexedSortKey(ParameterExpression parameter, string tableName, FieldDefinition field, IQueryable<RecordFieldValue> index)
    {
        var row = Expression.Parameter(typeof(RecordFieldValue), "v");
        var filter = Expression.Lambda<Func<RecordFieldValue, bool>>(
            Expression.AndAlso(
                Expression.AndAlso(
                    Expression.Equal(Expression.Property(row, nameof(RecordFieldValue.TableName)), Expression.Constant(tableName)),
                    Expression.Equal(Expression.Property(row, nameof(RecordFieldValue.RecordId)), Expression.Property(parameter, nameof(Entity.Id)))),
                Expression.Equal(Expression.Property(row, nameof(RecordFieldValue.Field)), Expression.Constant(field.Name))),
            row);
        var column = FieldIndexNormalizer.ColumnFor(field.Type) switch
        {
            FieldIndexColumn.Number => nameof(RecordFieldValue.NumberValue),
            FieldIndexColumn.Date => nameof(RecordFieldValue.DateValue),
            _ => nameof(RecordFieldValue.TextValue),
        };
        var selector = Expression.Lambda(Expression.Property(row, column), row);
        var where = Expression.Call(s_where.MakeGenericMethod(typeof(RecordFieldValue)), index.Expression, Expression.Quote(filter));
        var select = Expression.Call(s_select.MakeGenericMethod(typeof(RecordFieldValue), selector.ReturnType), where, Expression.Quote(selector));
        var first = Expression.Call(s_firstOrDefault.MakeGenericMethod(selector.ReturnType), select);
        return Expression.Lambda(first, parameter);
    }

    private static MethodInfo QueryableMethod(string name, int parameterCount) =>
        typeof(Queryable).GetMethods().First(m => m.Name == name && m.GetParameters().Length == parameterCount
            && m.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2);
}
