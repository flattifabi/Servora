using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Domain.Identity;

namespace Servora.Infrastructure.Changes;

/// <summary>Shared helpers of the service management seeders (idempotent group creation).</summary>
internal static class ServiceManagementSeedHelper
{
    /// <summary>Normalized login name of the bootstrap administrator.</summary>
    public const string AdminNormalizedUserName = "ADMIN";

    /// <summary>Gets a group by name or creates it. Newly created approval groups get the administrator as first member.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="name">The group name.</param>
    /// <param name="type">The group type.</param>
    /// <param name="description">The description.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The group identifier.</returns>
    public static async Task<Guid> EnsureGroupAsync(IAppDbContext db, string name, GroupType type, string description, CancellationToken cancellationToken)
    {
        var existing = await db.Set<Group>().Where(g => g.Name == name).Select(g => (Guid?)g.Id).FirstOrDefaultAsync(cancellationToken);
        if (existing is { } id)
        {
            return id;
        }

        var group = new Group { Name = name, Type = type, Description = description, IsActive = true };
        db.Set<Group>().Add(group);
        var adminId = await db.Set<User>().Where(u => u.NormalizedUserName == AdminNormalizedUserName).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(cancellationToken);
        if (adminId is { } admin && type == GroupType.Approval)
        {
            db.Set<GroupMember>().Add(new GroupMember { GroupId = group.Id, UserId = admin });
        }

        await db.SaveChangesAsync(cancellationToken);
        return group.Id;
    }
}
