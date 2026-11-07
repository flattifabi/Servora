using Servora.Application.Common;

namespace Servora.Application.Itsm;

/// <summary>Builds the personalised "My Work" workspace of the current user.</summary>
public interface IMyWorkService
{
    /// <summary>Gets the workspace data. Sections the user has no permission for are returned empty.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The dashboard, or a forbidden result for non-user actors.</returns>
    Task<Result<MyWorkDashboard>> GetAsync(CancellationToken cancellationToken = default);
}
