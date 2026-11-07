using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Domain.Identity;

namespace Servora.Application.Identity;

/// <summary>Queries that resolve which permission keys a role or group grant would hand out (input for the escalation guard).</summary>
public static class IdentityGrants
{
    /// <summary>Gets the distinct permission keys of the given roles.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="roleIds">The role identifiers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The permission keys.</returns>
    public static async Task<IReadOnlyList<string>> GetRolePermissionsAsync(IAppDbContext db, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        if (roleIds.Count == 0)
        {
            return [];
        }

        return await db.Set<RolePermission>().AsNoTracking()
            .Where(p => roleIds.Contains(p.RoleId))
            .Select(p => p.Permission)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    /// <summary>Gets the distinct permission keys granted to members of a group (through its roles).</summary>
    /// <param name="db">The database context.</param>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The permission keys.</returns>
    public static async Task<IReadOnlyList<string>> GetGroupPermissionsAsync(IAppDbContext db, Guid groupId, CancellationToken cancellationToken)
    {
        var roleIds = await db.Set<GroupRole>().AsNoTracking().Where(r => r.GroupId == groupId).Select(r => r.RoleId).ToListAsync(cancellationToken);
        return await GetRolePermissionsAsync(db, roleIds, cancellationToken);
    }

    /// <summary>Gets the identifiers of the active members of a group.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user identifiers.</returns>
    public static async Task<IReadOnlyList<Guid>> GetGroupMemberIdsAsync(IAppDbContext db, Guid groupId, CancellationToken cancellationToken) =>
        await db.Set<GroupMember>().AsNoTracking().Where(m => m.GroupId == groupId).Select(m => m.UserId).ToListAsync(cancellationToken);
}
