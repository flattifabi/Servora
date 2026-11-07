using Servora.Application.Common;
using Servora.Application.Conditions;

namespace Servora.Application.Tables;

/// <summary>Type-erased data access for one table (typed entity set or custom records of one custom table).</summary>
internal interface ITableAccessor
{
    /// <summary>Queries one page of entities.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="table">The table.</param>
    /// <param name="query">The validated query.</param>
    /// <param name="context">The condition context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entities of the page and the total count.</returns>
    Task<(IReadOnlyList<object> Items, int Total)> QueryAsync(IAppDbContext db, TableDefinition table, RecordQuery query, ConditionContext context, CancellationToken cancellationToken);

    /// <summary>Finds an entity by identifier.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="table">The table.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="tracking">Whether the entity is tracked for updates.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entity or <see langword="null"/>.</returns>
    Task<object?> FindAsync(IAppDbContext db, TableDefinition table, Guid id, bool tracking, CancellationToken cancellationToken);

    /// <summary>Creates a new (detached) entity instance.</summary>
    /// <param name="table">The table.</param>
    /// <returns>The entity.</returns>
    object Create(TableDefinition table);

    /// <summary>Adds an entity to the context.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="entity">The entity.</param>
    void Add(IAppDbContext db, object entity);

    /// <summary>Marks an entity as deleted.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="entity">The entity.</param>
    void Remove(IAppDbContext db, object entity);

    /// <summary>Returns the identifiers that exist in the table.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="table">The table.</param>
    /// <param name="ids">The candidate identifiers (bounded by the caller).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The existing identifiers.</returns>
    Task<HashSet<Guid>> FindExistingAsync(IAppDbContext db, TableDefinition table, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Loads entities by identifier for display value resolution.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="table">The table.</param>
    /// <param name="ids">The identifiers (bounded by the caller).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entities.</returns>
    Task<IReadOnlyList<object>> LoadManyAsync(IAppDbContext db, TableDefinition table, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Gets the database maximum length of a typed text field.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="field">The field.</param>
    /// <returns>The maximum length or <see langword="null"/>.</returns>
    int? GetMaxLength(IAppDbContext db, FieldDefinition field);
}
