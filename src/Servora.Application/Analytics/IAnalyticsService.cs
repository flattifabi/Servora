using Servora.Application.Common;

namespace Servora.Application.Analytics;

/// <summary>
/// Computes the executive dashboard from the typed tables that exist at runtime. Modules that are not installed or
/// tables the user may not read yield explicit "module missing" / "restricted" widgets instead of errors.
/// Requires <c>analytics.read</c>.
/// </summary>
public interface IAnalyticsService
{
    /// <summary>Builds the executive dashboard.</summary>
    /// <param name="periodDays">The period in days (7..365).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The dashboard.</returns>
    Task<Result<ExecutiveDashboard>> GetExecutiveDashboardAsync(int periodDays = 30, CancellationToken cancellationToken = default);
}
