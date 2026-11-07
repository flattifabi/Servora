using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Domain.Cmdb;
using Servora.Domain.Identity;

namespace Servora.Application.Approvals;

/// <summary>Bounded batch look-ups of display names for references (users, groups, configuration items) used by the service management services.</summary>
public static class ReferenceNameResolver
{
    private const int MaxIds = 1000;

    /// <summary>Resolves user display names.</summary>
    /// <param name="db">An open database context.</param>
    /// <param name="ids">The user identifiers (nulls are ignored).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A dictionary from id to display name.</returns>
    public static async Task<Dictionary<Guid, string>> UsersAsync(IAppDbContext db, IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var list = Distinct(ids);
        return list.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Set<User>().AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);
    }

    /// <summary>Resolves group names.</summary>
    /// <param name="db">An open database context.</param>
    /// <param name="ids">The group identifiers (nulls are ignored).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A dictionary from id to name.</returns>
    public static async Task<Dictionary<Guid, string>> GroupsAsync(IAppDbContext db, IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var list = Distinct(ids);
        return list.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Set<Group>().AsNoTracking().Where(g => list.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.Name, cancellationToken);
    }

    /// <summary>Resolves configuration item names.</summary>
    /// <param name="db">An open database context.</param>
    /// <param name="ids">The CI identifiers (nulls are ignored).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A dictionary from id to name.</returns>
    public static async Task<Dictionary<Guid, string>> ConfigurationItemsAsync(IAppDbContext db, IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var list = Distinct(ids);
        return list.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Set<ConfigurationItem>().AsNoTracking().Where(c => list.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
    }

    /// <summary>Gets a name from a dictionary or <see langword="null"/>.</summary>
    /// <param name="names">The names.</param>
    /// <param name="id">The identifier.</param>
    /// <returns>The name.</returns>
    public static string? NameOf(this IReadOnlyDictionary<Guid, string> names, Guid? id) =>
        id is { } value && names.TryGetValue(value, out var name) ? name : null;

    private static List<Guid> Distinct(IEnumerable<Guid?> ids) =>
        ids.Where(i => i.HasValue && i.Value != Guid.Empty).Select(i => i!.Value).Distinct().Take(MaxIds).ToList();
}
