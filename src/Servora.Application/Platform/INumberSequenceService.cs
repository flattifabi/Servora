namespace Servora.Application.Platform;

/// <summary>Allocates human readable, unique record numbers such as <c>INC0001001</c>.</summary>
public interface INumberSequenceService
{
    /// <summary>Allocates the next number for a table. Creates the sequence on first use.</summary>
    /// <param name="tableName">The technical table name.</param>
    /// <param name="defaultPrefix">The prefix used when the sequence does not exist yet, for example <c>INC</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The formatted number.</returns>
    Task<string> NextAsync(string tableName, string defaultPrefix, CancellationToken cancellationToken = default);
}
