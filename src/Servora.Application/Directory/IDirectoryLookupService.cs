namespace Servora.Application.Directory;

/// <summary>Lightweight look-ups of users, groups and configuration items for reference pickers and display values.</summary>
public interface IDirectoryLookupService
{
    /// <summary>Searches active users by name, login or e-mail.</summary>
    /// <param name="term">The search term (may be empty to list the first users).</param>
    /// <param name="take">The maximum number of results (clamped to 50).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching users.</returns>
    Task<IReadOnlyList<LookupItem>> SearchUsersAsync(string? term, int take = 10, CancellationToken cancellationToken = default);

    /// <summary>Searches active groups by name.</summary>
    /// <param name="term">The search term.</param>
    /// <param name="take">The maximum number of results (clamped to 50).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching groups.</returns>
    Task<IReadOnlyList<LookupItem>> SearchGroupsAsync(string? term, int take = 10, CancellationToken cancellationToken = default);

    /// <summary>Searches configuration items by name, FQDN or IP.</summary>
    /// <param name="term">The search term.</param>
    /// <param name="take">The maximum number of results (clamped to 50).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching CIs.</returns>
    Task<IReadOnlyList<LookupItem>> SearchConfigurationItemsAsync(string? term, int take = 10, CancellationToken cancellationToken = default);

    /// <summary>Resolves display names for a set of user ids.</summary>
    /// <param name="ids">The user ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A dictionary from id to item.</returns>
    Task<IReadOnlyDictionary<Guid, LookupItem>> GetUsersAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Resolves display names for a set of group ids.</summary>
    /// <param name="ids">The group ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A dictionary from id to item.</returns>
    Task<IReadOnlyDictionary<Guid, LookupItem>> GetGroupsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Resolves display names for a set of configuration item ids.</summary>
    /// <param name="ids">The CI ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A dictionary from id to item.</returns>
    Task<IReadOnlyDictionary<Guid, LookupItem>> GetConfigurationItemsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
}
