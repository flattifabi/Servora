using Servora.Application.Common;

namespace Servora.Application.Tables;

/// <summary>
/// Generic, permission-checked CRUD over any table using field dictionaries. Backs the generic list / form views, the
/// Table REST API, flow actions, MCP tools and imports. Enforces table permissions
/// (<c>table.&lt;name&gt;.read|create|write|delete</c> or the owning module's permission) for the current actor.
/// </summary>
public interface IRecordService
{
    /// <summary>Queries records.</summary>
    /// <param name="tableName">The technical table name.</param>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of records as field dictionaries (always including <c>Id</c>).</returns>
    Task<Result<PagedResult<IReadOnlyDictionary<string, object?>>>> QueryAsync(string tableName, RecordQuery query, CancellationToken cancellationToken = default);

    /// <summary>Gets a single record.</summary>
    /// <param name="tableName">The technical table name.</param>
    /// <param name="id">The record identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The record values.</returns>
    Task<Result<IReadOnlyDictionary<string, object?>>> GetAsync(string tableName, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a record. Unknown and read-only fields are rejected.</summary>
    /// <param name="tableName">The technical table name.</param>
    /// <param name="values">The field values (invariant strings, numbers, booleans or JSON elements).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifier of the new record.</returns>
    Task<Result<Guid>> CreateAsync(string tableName, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken = default);

    /// <summary>Updates fields of a record.</summary>
    /// <param name="tableName">The technical table name.</param>
    /// <param name="id">The record identifier.</param>
    /// <param name="values">The field values to change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UpdateAsync(string tableName, Guid id, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken = default);

    /// <summary>Deletes a record.</summary>
    /// <param name="tableName">The technical table name.</param>
    /// <param name="id">The record identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(string tableName, Guid id, CancellationToken cancellationToken = default);
}
