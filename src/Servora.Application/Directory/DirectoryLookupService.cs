using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Domain.Cmdb;
using Servora.Domain.Identity;

namespace Servora.Application.Directory;

/// <summary>Default <see cref="IDirectoryLookupService"/> implementation.</summary>
/// <param name="dbFactory">The database context factory.</param>
public sealed class DirectoryLookupService(IAppDbContextFactory dbFactory) : IDirectoryLookupService
{
    private const int MaxTake = 50;

    /// <inheritdoc />
    public async Task<IReadOnlyList<LookupItem>> SearchUsersAsync(string? term, int take = 10, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<User>().AsNoTracking().Where(u => u.IsActive && u.Source != UserSource.Service);
        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim().ToLower();
            query = query.Where(u => u.DisplayName.ToLower().Contains(t) || u.UserName.ToLower().Contains(t) || (u.Email != null && u.Email.ToLower().Contains(t)));
        }

        return await query.OrderBy(u => u.DisplayName)
            .Take(Math.Clamp(take, 1, MaxTake))
            .Select(u => new LookupItem(u.Id, u.DisplayName, u.Email ?? u.UserName))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LookupItem>> SearchGroupsAsync(string? term, int take = 10, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<Group>().AsNoTracking().Where(g => g.IsActive);
        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim().ToLower();
            query = query.Where(g => g.Name.ToLower().Contains(t));
        }

        var groups = await query.OrderBy(g => g.Name).Take(Math.Clamp(take, 1, MaxTake)).ToListAsync(cancellationToken);
        return groups.Select(g => new LookupItem(g.Id, g.Name, g.Type.ToString())).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LookupItem>> SearchConfigurationItemsAsync(string? term, int take = 10, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<ConfigurationItem>().AsNoTracking().Where(c => c.OperationalStatus != OperationalStatus.Retired);
        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(t) || (c.Fqdn != null && c.Fqdn.ToLower().Contains(t)) || (c.IpAddress != null && c.IpAddress.Contains(t)));
        }

        return await query.OrderBy(c => c.Name)
            .Take(Math.Clamp(take, 1, MaxTake))
            .Select(c => new LookupItem(c.Id, c.Name, c.ClassName))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, LookupItem>> GetUsersAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var set = ids.Distinct().ToList();
        if (set.Count == 0)
        {
            return new Dictionary<Guid, LookupItem>();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<User>().AsNoTracking().Where(u => set.Contains(u.Id))
            .Select(u => new LookupItem(u.Id, u.DisplayName, u.Email ?? u.UserName))
            .ToDictionaryAsync(u => u.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, LookupItem>> GetGroupsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var set = ids.Distinct().ToList();
        if (set.Count == 0)
        {
            return new Dictionary<Guid, LookupItem>();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var groups = await db.Set<Group>().AsNoTracking().Where(g => set.Contains(g.Id)).ToListAsync(cancellationToken);
        return groups.ToDictionary(g => g.Id, g => new LookupItem(g.Id, g.Name, g.Type.ToString()));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, LookupItem>> GetConfigurationItemsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var set = ids.Distinct().ToList();
        if (set.Count == 0)
        {
            return new Dictionary<Guid, LookupItem>();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<ConfigurationItem>().AsNoTracking().Where(c => set.Contains(c.Id))
            .Select(c => new LookupItem(c.Id, c.Name, c.ClassName))
            .ToDictionaryAsync(c => c.Id, cancellationToken);
    }
}
