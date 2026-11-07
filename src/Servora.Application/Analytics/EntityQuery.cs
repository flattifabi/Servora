using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Domain.Common;

namespace Servora.Application.Analytics;

/// <summary>
/// Provider-neutral, bounded queries against entity types that are only known at runtime (tables of modules that may
/// or may not be installed). Builds expression trees over CLR properties so EF Core translates them on SQLite, SQL Server
/// and PostgreSQL alike; only key / value columns are projected.
/// </summary>
internal static class EntityQuery
{
    private static readonly MethodInfo s_project = typeof(EntityQuery).GetMethod(nameof(ProjectCoreAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo s_count = typeof(EntityQuery).GetMethod(nameof(CountCoreAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo s_display = typeof(EntityQuery).GetMethod(nameof(ResolveDisplayCoreAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly PropertyInfo s_keyProperty = typeof(ReportProjection).GetProperty(nameof(ReportProjection.Key))!;
    private static readonly PropertyInfo s_valueProperty = typeof(ReportProjection).GetProperty(nameof(ReportProjection.Value))!;

    /// <summary>Checks whether an entity type is part of the EF model of the context.</summary>
    /// <param name="db">The context.</param>
    /// <param name="entityType">The CLR type.</param>
    /// <returns><see langword="true"/> when the type can be queried.</returns>
    public static bool IsMapped(IAppDbContext db, Type? entityType) =>
        entityType is not null && db is DbContext context && context.Model.FindEntityType(entityType) is not null;

    /// <summary>Finds a mapped entity type by CLR type name (for modules whose tables are not record tables).</summary>
    /// <param name="db">The context.</param>
    /// <param name="clrNames">Candidate CLR type names.</param>
    /// <returns>The first mapped type found, or <see langword="null"/>.</returns>
    public static Type? FindMappedType(IAppDbContext db, params string[] clrNames)
    {
        if (db is not DbContext context)
        {
            return null;
        }

        var types = context.Model.GetEntityTypes().Select(e => e.ClrType).ToList();
        return clrNames.Select(name => types.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))).FirstOrDefault(t => t is not null);
    }

    /// <summary>Gets a public instance property (case-insensitive) or null.</summary>
    /// <param name="type">The type.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The property.</returns>
    public static PropertyInfo? FindProperty(Type type, string? name) =>
        string.IsNullOrEmpty(name) ? null : type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

    /// <summary>Builds <c>x =&gt; x.Property &gt;= since</c> for a date property.</summary>
    /// <param name="entityType">The entity type.</param>
    /// <param name="dateProperty">The <see cref="DateTimeOffset"/> property.</param>
    /// <param name="since">The lower bound.</param>
    /// <returns>The predicate, or null when the property does not exist or is not a date.</returns>
    public static LambdaExpression? Since(Type entityType, string dateProperty, DateTimeOffset since)
    {
        var property = FindProperty(entityType, dateProperty);
        if (property is null || (Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType) != typeof(DateTimeOffset))
        {
            return null;
        }

        var parameter = Expression.Parameter(entityType, "x");
        var body = Expression.GreaterThanOrEqual(Expression.Property(parameter, property), Expression.Constant(since, property.PropertyType));
        return Expression.Lambda(body, parameter);
    }

    /// <summary>Builds <c>x =&gt; x.Property == value</c>.</summary>
    /// <param name="entityType">The entity type.</param>
    /// <param name="propertyName">The property.</param>
    /// <param name="value">The value (converted to the property type).</param>
    /// <returns>The predicate, or null when the property does not exist.</returns>
    public static LambdaExpression? EqualTo(Type entityType, string propertyName, object? value)
    {
        var property = FindProperty(entityType, propertyName);
        if (property is null)
        {
            return null;
        }

        var parameter = Expression.Parameter(entityType, "x");
        var body = Expression.Equal(Expression.Property(parameter, property), Expression.Constant(value, property.PropertyType));
        return Expression.Lambda(body, parameter);
    }

    /// <summary>Builds <c>x =&gt; x.Property != null</c>.</summary>
    /// <param name="entityType">The entity type.</param>
    /// <param name="propertyName">The nullable property.</param>
    /// <returns>The predicate, or null when the property does not exist or is not nullable.</returns>
    public static LambdaExpression? NotNull(Type entityType, string propertyName)
    {
        var property = FindProperty(entityType, propertyName);
        if (property is null || (property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) is null))
        {
            return null;
        }

        var parameter = Expression.Parameter(entityType, "x");
        var body = Expression.NotEqual(Expression.Property(parameter, property), Expression.Constant(null, property.PropertyType));
        return Expression.Lambda(body, parameter);
    }

    /// <summary>Projects key / value columns of matching rows (at most <paramref name="max"/> rows).</summary>
    /// <param name="db">The context.</param>
    /// <param name="entityType">The entity type.</param>
    /// <param name="keyProperty">The key property, or null.</param>
    /// <param name="valueProperty">The value property, or null.</param>
    /// <param name="filters">Predicates (<c>Expression&lt;Func&lt;T, bool&gt;&gt;</c>) combined with AND.</param>
    /// <param name="max">The maximum number of rows.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows.</returns>
    public static Task<List<ReportProjection>> ProjectAsync(IAppDbContext db, Type entityType, string? keyProperty, string? valueProperty, IEnumerable<LambdaExpression?> filters, int max, CancellationToken cancellationToken) =>
        (Task<List<ReportProjection>>)s_project.MakeGenericMethod(entityType).Invoke(null, [db, keyProperty, valueProperty, filters.OfType<LambdaExpression>().ToList(), max, cancellationToken])!;

    /// <summary>Counts matching rows.</summary>
    /// <param name="db">The context.</param>
    /// <param name="entityType">The entity type.</param>
    /// <param name="filters">Predicates combined with AND.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count.</returns>
    public static Task<int> CountAsync(IAppDbContext db, Type entityType, IEnumerable<LambdaExpression?> filters, CancellationToken cancellationToken) =>
        (Task<int>)s_count.MakeGenericMethod(entityType).Invoke(null, [db, filters.OfType<LambdaExpression>().ToList(), cancellationToken])!;

    /// <summary>Resolves display values of referenced records (at most 1,000 ids).</summary>
    /// <param name="db">The context.</param>
    /// <param name="entityType">The referenced entity type (must derive from <see cref="Entity"/>).</param>
    /// <param name="displayField">The display property.</param>
    /// <param name="ids">The record ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Display values keyed by id.</returns>
    public static async Task<Dictionary<Guid, string>> ResolveDisplayAsync(IAppDbContext db, Type entityType, string displayField, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0 || !typeof(Entity).IsAssignableFrom(entityType) || FindProperty(entityType, displayField) is null || !IsMapped(db, entityType))
        {
            return [];
        }

        return await (Task<Dictionary<Guid, string>>)s_display.MakeGenericMethod(entityType).Invoke(null, [db, displayField, ids.Take(1000).ToList(), cancellationToken])!;
    }

    private static async Task<List<ReportProjection>> ProjectCoreAsync<T>(IAppDbContext db, string? keyProperty, string? valueProperty, List<LambdaExpression> filters, int max, CancellationToken cancellationToken)
        where T : class
    {
        IQueryable<T> query = db.Set<T>().AsNoTracking();
        foreach (var filter in filters)
        {
            query = query.Where((Expression<Func<T, bool>>)filter);
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        var key = FindProperty(typeof(T), keyProperty);
        var value = FindProperty(typeof(T), valueProperty);
        var body = Expression.MemberInit(
            Expression.New(typeof(ReportProjection)),
            Expression.Bind(s_keyProperty, key is null ? Expression.Constant(null, typeof(object)) : Expression.Convert(Expression.Property(parameter, key), typeof(object))),
            Expression.Bind(s_valueProperty, value is null ? Expression.Constant(null, typeof(object)) : Expression.Convert(Expression.Property(parameter, value), typeof(object))));
        var selector = Expression.Lambda<Func<T, ReportProjection>>(body, parameter);
        return await query.Select(selector).Take(max).ToListAsync(cancellationToken);
    }

    private static async Task<int> CountCoreAsync<T>(IAppDbContext db, List<LambdaExpression> filters, CancellationToken cancellationToken)
        where T : class
    {
        IQueryable<T> query = db.Set<T>().AsNoTracking();
        foreach (var filter in filters)
        {
            query = query.Where((Expression<Func<T, bool>>)filter);
        }

        return await query.CountAsync(cancellationToken);
    }

    private static async Task<Dictionary<Guid, string>> ResolveDisplayCoreAsync<T>(IAppDbContext db, string displayField, List<Guid> ids, CancellationToken cancellationToken)
        where T : Entity
    {
        var rows = await ProjectCoreAsync<T>(db, nameof(Entity.Id), displayField, [(Expression<Func<T, bool>>)(x => ids.Contains(x.Id))], ids.Count, cancellationToken);
        var result = new Dictionary<Guid, string>();
        foreach (var row in rows)
        {
            if (row.Key is Guid id)
            {
                result[id] = Convert.ToString(row.Value, System.Globalization.CultureInfo.InvariantCulture) is { Length: > 0 } text ? text : id.ToString("N")[..8];
            }
        }

        return result;
    }
}
