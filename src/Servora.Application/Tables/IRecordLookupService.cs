using Servora.Application.Directory;

namespace Servora.Application.Tables;

/// <summary>Searches and resolves records of any table for reference pickers (permission-checked).</summary>
public interface IRecordLookupService
{
    /// <summary>Searches records by their display value and text fields.</summary>
    /// <param name="tableName">The referenced table.</param>
    /// <param name="term">The search term.</param>
    /// <param name="take">The maximum number of results (clamped to 50).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matches (empty when the table does not exist or is not readable).</returns>
    Task<IReadOnlyList<LookupItem>> SearchAsync(string tableName, string? term, int take = 10, CancellationToken cancellationToken = default);

    /// <summary>Resolves the display value of a record.</summary>
    /// <param name="tableName">The referenced table.</param>
    /// <param name="id">The record identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The item or <see langword="null"/>.</returns>
    Task<LookupItem?> ResolveAsync(string tableName, Guid id, CancellationToken cancellationToken = default);
}
