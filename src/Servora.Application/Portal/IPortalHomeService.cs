using Servora.Application.Common;

namespace Servora.Application.Portal;

/// <summary>Portal home page and hero search. Requires <c>portal.access</c>; personal data is limited to the user's own items.</summary>
public interface IPortalHomeService
{
    /// <summary>Gets the home page data of the current user.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The data.</returns>
    Task<Result<PortalHomeData>> GetHomeAsync(CancellationToken cancellationToken = default);

    /// <summary>Counts the current user's open requests and incidents (navigation badge).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of open items.</returns>
    Task<Result<int>> CountOpenItemsAsync(CancellationToken cancellationToken = default);

    /// <summary>Searches published knowledge, orderable catalog items and the user's own items.</summary>
    /// <param name="query">The free-text query.</param>
    /// <param name="take">The maximum number of results per section (1..20).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The results.</returns>
    Task<Result<PortalSearchResults>> SearchAsync(string? query, int take = 6, CancellationToken cancellationToken = default);
}
