using Servora.Application.Common;

namespace Servora.Application.Platform;

/// <summary>Resolves human readable record numbers (<c>INC0001001</c>, <c>KB0001234</c>, custom prefixes) to records.</summary>
public interface IRecordNumberResolver
{
    /// <summary>Resolves a record number.</summary>
    /// <param name="number">The number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The location, not found, or forbidden when the table is not readable.</returns>
    Task<Result<RecordLocation>> ResolveAsync(string number, CancellationToken cancellationToken = default);
}
