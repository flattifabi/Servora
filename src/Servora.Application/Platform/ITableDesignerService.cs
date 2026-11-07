using Servora.Application.Common;

namespace Servora.Application.Platform;

/// <summary>Studio operations: custom tables, custom fields, form layouts and list columns.</summary>
public interface ITableDesignerService
{
    /// <summary>Lists all tables (permission <c>platform.tables.manage</c>).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tables.</returns>
    Task<Result<IReadOnlyList<TableSummary>>> ListTablesAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the editable properties of a custom table.</summary>
    /// <param name="name">The table name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The properties.</returns>
    Task<Result<CustomTableInput>> GetCustomTableAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Creates a custom table.</summary>
    /// <param name="input">The properties.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The technical name of the new table.</returns>
    Task<Result<string>> CreateTableAsync(CustomTableInput input, CancellationToken cancellationToken = default);

    /// <summary>Updates a custom table.</summary>
    /// <param name="name">The table name.</param>
    /// <param name="input">The properties.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UpdateTableAsync(string name, CustomTableInput input, CancellationToken cancellationToken = default);

    /// <summary>Deletes a custom table with all its records, fields and layout.</summary>
    /// <param name="name">The table name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteTableAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Gets the editable properties of a custom field.</summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="fieldName">The field name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The properties.</returns>
    Task<Result<CustomFieldInput>> GetFieldAsync(string tableName, string fieldName, CancellationToken cancellationToken = default);

    /// <summary>Adds a custom field to a custom table or a typed table that supports custom fields.</summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="input">The properties.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The technical name of the new field.</returns>
    Task<Result<string>> AddFieldAsync(string tableName, CustomFieldInput input, CancellationToken cancellationToken = default);

    /// <summary>Updates a custom field (the type cannot change).</summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="fieldName">The field name.</param>
    /// <param name="input">The properties.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UpdateFieldAsync(string tableName, string fieldName, CustomFieldInput input, CancellationToken cancellationToken = default);

    /// <summary>Deletes a custom field (stored values are ignored afterwards).</summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="fieldName">The field name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteFieldAsync(string tableName, string fieldName, CancellationToken cancellationToken = default);

    /// <summary>Gets the form layout and list columns (read access to the table or <c>platform.tables.manage</c>).</summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The layout (a generated default when none was saved).</returns>
    Task<Result<TableLayoutModel>> GetLayoutAsync(string tableName, CancellationToken cancellationToken = default);

    /// <summary>Saves the form layout and list columns.</summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="layout">The layout.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> SaveLayoutAsync(string tableName, TableLayoutModel layout, CancellationToken cancellationToken = default);
}
