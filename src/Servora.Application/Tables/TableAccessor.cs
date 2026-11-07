using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Domain.Common;
using Servora.Domain.Platform;

namespace Servora.Application.Tables;

/// <summary>
/// Generic <see cref="ITableAccessor"/> over an EF entity set. For <see cref="CustomRecord"/> every query is scoped to
/// the custom table's name; custom fields are matched, searched and sorted through the <see cref="RecordFieldValue"/> index.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
internal sealed class TableAccessor<T> : ITableAccessor
    where T : Entity, new()
{
    /// <inheritdoc />
    public async Task<(IReadOnlyList<object> Items, int Total)> QueryAsync(IAppDbContext db, TableDefinition table, RecordQuery query, ConditionContext context, CancellationToken cancellationToken)
    {
        var index = db.Set<RecordFieldValue>();
        var options = new ConditionQueryOptions
        {
            TableName = table.Name,
            FieldValues = index,
            ResolveIndexedField = name => table.FindField(name) is { IsCustom: true } field ? field : null,
        };

        var source = Scope(db.Set<T>().AsNoTracking(), table);
        if (query.Condition is { IsEmpty: false } condition)
        {
            source = source.Where(ConditionExpressionBuilder.Build<T>(condition, context, options));
        }

        if (!string.IsNullOrWhiteSpace(query.Page.Search))
        {
            source = RecordQueryBuilder.ApplySearch(source, table, query.Page.Search.Trim(), options);
        }

        var total = await source.CountAsync(cancellationToken);
        var ordered = RecordQueryBuilder.ApplySort(source, table, query.Page.SortBy, query.Page.SortDescending, index);
        var items = await ordered.Skip(query.Page.Skip).Take(query.Page.PageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    /// <inheritdoc />
    public async Task<object?> FindAsync(IAppDbContext db, TableDefinition table, Guid id, bool tracking, CancellationToken cancellationToken)
    {
        IQueryable<T> source = db.Set<T>();
        if (!tracking)
        {
            source = source.AsNoTracking();
        }

        return await Scope(source, table).FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public object Create(TableDefinition table)
    {
        var entity = new T();
        if (entity is CustomRecord record)
        {
            record.TableName = table.Name;
        }

        return entity;
    }

    /// <inheritdoc />
    public void Add(IAppDbContext db, object entity) => db.Set<T>().Add((T)entity);

    /// <inheritdoc />
    public void Remove(IAppDbContext db, object entity) => db.Set<T>().Remove((T)entity);

    /// <inheritdoc />
    public async Task<HashSet<Guid>> FindExistingAsync(IAppDbContext db, TableDefinition table, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var list = ids.Distinct().ToList();
        var found = await Scope(db.Set<T>().AsNoTracking(), table).Where(e => list.Contains(e.Id)).Select(e => e.Id).ToListAsync(cancellationToken);
        return [.. found];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<object>> LoadManyAsync(IAppDbContext db, TableDefinition table, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var list = ids.Distinct().ToList();
        return await Scope(db.Set<T>().AsNoTracking(), table).Where(e => list.Contains(e.Id)).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public int? GetMaxLength(IAppDbContext db, FieldDefinition field) =>
        field.IsCustom ? field.MaxLength : db.Set<T>().EntityType.FindProperty(field.Name)?.GetMaxLength() ?? field.MaxLength;

    private static IQueryable<T> Scope(IQueryable<T> source, TableDefinition table)
    {
        if (typeof(T) != typeof(CustomRecord))
        {
            return source;
        }

        var name = table.Name;
        return (IQueryable<T>)((IQueryable<CustomRecord>)source).Where(r => r.TableName == name);
    }
}
