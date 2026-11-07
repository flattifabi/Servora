using Servora.Application.Common;

namespace Servora.Application.Portal;

/// <summary>
/// Announcements on the portal home page. Reading the live announcements requires <c>portal.access</c>; the
/// administration (list all, create, edit, delete) requires <c>portal.manage</c> and is audited.
/// </summary>
public interface IPortalAnnouncementService
{
    /// <summary>Gets the announcements that are active and inside their display window (outages first).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The live announcements.</returns>
    Task<Result<IReadOnlyList<PortalAnnouncementView>>> GetLiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists all announcements for the administration (newest first, max. 200).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The announcements.</returns>
    Task<Result<IReadOnlyList<PortalAnnouncementView>>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates or updates an announcement.</summary>
    /// <param name="id">The identifier of the announcement to update, or <see langword="null"/> to create one.</param>
    /// <param name="input">The fields.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The announcement identifier.</returns>
    Task<Result<Guid>> SaveAsync(Guid? id, PortalAnnouncementInput input, CancellationToken cancellationToken = default);

    /// <summary>Deletes an announcement.</summary>
    /// <param name="id">The announcement identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
