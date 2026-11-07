using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Servora.Application.Common;
using Servora.Domain.Identity;

namespace Servora.Application.Security;

/// <summary>
/// Default <see cref="IPermissionService"/>. Effective permissions = permissions of direct roles + roles granted
/// through active group memberships. Cached for 60 seconds per user; mutations must call <see cref="Invalidate"/>.
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="cache">The memory cache.</param>
public sealed class PermissionService(IAppDbContextFactory dbFactory, IMemoryCache cache) : IPermissionService
{
    private static readonly TimeSpan s_cacheDuration = TimeSpan.FromSeconds(60);
    private static int s_generation;

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var (_, permissions) = await LoadAsync(userId, cancellationToken);
        return permissions;
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var (roles, _) = await LoadAsync(userId, cancellationToken);
        return roles;
    }

    /// <inheritdoc />
    public async Task<bool> UserHasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default)
    {
        var permissions = await GetUserPermissionsAsync(userId, cancellationToken);
        return PermissionMatcher.IsGranted(permissions, permission);
    }

    /// <inheritdoc />
    public void Invalidate(Guid? userId = null)
    {
        if (userId is null)
        {
            Interlocked.Increment(ref s_generation);
        }
        else
        {
            cache.Remove(CacheKey(userId.Value));
        }
    }

    private static string CacheKey(Guid userId) => $"perm:{Volatile.Read(ref s_generation)}:{userId:N}";

    private async Task<(IReadOnlySet<string> Roles, IReadOnlySet<string> Permissions)> LoadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var key = CacheKey(userId);
        if (cache.TryGetValue(key, out (IReadOnlySet<string>, IReadOnlySet<string>) cached))
        {
            return cached;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var active = await db.Set<User>().AsNoTracking().Where(u => u.Id == userId).Select(u => u.IsActive).FirstOrDefaultAsync(cancellationToken);
        var roleIds = new HashSet<Guid>();
        if (active)
        {
            roleIds.UnionWith(await db.Set<UserRole>().AsNoTracking().Where(r => r.UserId == userId).Select(r => r.RoleId).ToListAsync(cancellationToken));
            var groupIds = await db.Set<GroupMember>().AsNoTracking()
                .Where(m => m.UserId == userId && m.Group!.IsActive)
                .Select(m => m.GroupId)
                .ToListAsync(cancellationToken);
            if (groupIds.Count > 0)
            {
                roleIds.UnionWith(await db.Set<GroupRole>().AsNoTracking().Where(r => groupIds.Contains(r.GroupId)).Select(r => r.RoleId).ToListAsync(cancellationToken));
            }
        }

        var roles = await db.Set<Role>().AsNoTracking().Where(r => roleIds.Contains(r.Id)).Select(r => r.Name).ToListAsync(cancellationToken);
        var permissions = await db.Set<RolePermission>().AsNoTracking().Where(p => roleIds.Contains(p.RoleId)).Select(p => p.Permission).ToListAsync(cancellationToken);
        var result = ((IReadOnlySet<string>)roles.ToHashSet(StringComparer.OrdinalIgnoreCase), (IReadOnlySet<string>)permissions.ToHashSet(StringComparer.Ordinal));
        cache.Set(key, result, s_cacheDuration);
        return result;
    }
}
