using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Identity;

namespace Servora.Application.Identity.Admin;

/// <summary>
/// Protects against locking everybody out: identifies the active users holding the global wildcard <c>*</c>
/// (directly or through an active group) so the last one cannot be deactivated, deleted or stripped of the role.
/// </summary>
public static class SuperAdminGuard
{
    /// <summary>The message returned when an operation would remove the last administrator.</summary>
    public const string LastAdministratorMessage = "This would remove the last active administrator. Grant the administrator role to another active user first.";

    /// <summary>Gets the identifiers of active users with the global wildcard permission.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user identifiers.</returns>
    public static async Task<HashSet<Guid>> GetActiveSuperAdminIdsAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var roleIds = await db.Set<RolePermission>().AsNoTracking().Where(p => p.Permission == Permissions.All).Select(p => p.RoleId).ToListAsync(cancellationToken);
        if (roleIds.Count == 0)
        {
            return [];
        }

        var direct = await db.Set<UserRole>().AsNoTracking().Where(r => roleIds.Contains(r.RoleId)).Select(r => r.UserId).ToListAsync(cancellationToken);
        var groupIds = await db.Set<GroupRole>().AsNoTracking().Where(r => roleIds.Contains(r.RoleId)).Select(r => r.GroupId).ToListAsync(cancellationToken);
        var viaGroups = groupIds.Count == 0
            ? []
            : await db.Set<GroupMember>().AsNoTracking().Where(m => groupIds.Contains(m.GroupId) && m.Group!.IsActive).Select(m => m.UserId).ToListAsync(cancellationToken);
        var candidates = direct.Concat(viaGroups).Distinct().ToList();
        var active = await db.Set<User>().AsNoTracking().Where(u => candidates.Contains(u.Id) && u.IsActive).Select(u => u.Id).ToListAsync(cancellationToken);
        return active.ToHashSet();
    }

    /// <summary>Checks whether removing a user's administrator access would leave no active administrator.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="userId">The user that loses administrator access.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the user is the last active administrator.</returns>
    public static async Task<bool> IsLastSuperAdminAsync(IAppDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        var ids = await GetActiveSuperAdminIdsAsync(db, cancellationToken);
        return ids.Count == 1 && ids.Contains(userId);
    }
}
