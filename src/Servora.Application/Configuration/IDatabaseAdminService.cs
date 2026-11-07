using Servora.Application.Common;

namespace Servora.Application.Configuration;

/// <summary>Database information and connection test for administrators (<c>admin.database.manage</c>).</summary>
public interface IDatabaseAdminService
{
    /// <summary>Inspects the active database.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The information.</returns>
    Task<Result<DatabaseInfo>> GetInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>Opens a new connection to the active database and measures the round trip; audited.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The elapsed time on success, otherwise a user-safe error.</returns>
    Task<Result<TimeSpan>> TestConnectionAsync(CancellationToken cancellationToken = default);
}
