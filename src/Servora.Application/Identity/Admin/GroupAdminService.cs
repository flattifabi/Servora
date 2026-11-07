using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Identity;
using Servora.Domain.Observability;
using Group = Servora.Domain.Identity.Group;

namespace Servora.Application.Identity.Admin;

/// <summary>Default <see cref="IGroupAdminService"/>.</summary>
/// <param name="currentUser">The current actor.</param>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="permissions">The permission service (cache invalidation).</param>
/// <param name="audit">The audit service.</param>
public sealed partial class GroupAdminService(
    ICurrentUser currentUser,
    IAppDbContextFactory dbFactory,
    IPermissionService permissions,
    IAuditService audit) : IGroupAdminService
{
    private const int MaxMembersShown = 500;
    private const int MaxMembersPerCall = 200;

    /// <inheritdoc />
    public async Task<Result<PagedResult<GroupListItem>>> ListAsync(GroupListQuery query, PageRequest page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ReadUsers, cancellationToken))
        {
            return Result<PagedResult<GroupListItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var groups = db.Set<Group>().AsNoTracking();
        if (query.Type is { } type)
        {
            groups = groups.Where(g => g.Type == type);
        }

        if (query.ActiveOnly is { } active)
        {
            groups = groups.Where(g => g.IsActive == active);
        }

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim().ToLower();
            groups = groups.Where(g => g.Name.ToLower().Contains(term) || (g.Description != null && g.Description.ToLower().Contains(term)) || (g.Email != null && g.Email.ToLower().Contains(term)));
        }

        groups = (page.SortBy, page.SortDescending) switch
        {
            ("Type", false) => groups.OrderBy(g => g.Type).ThenBy(g => g.Name),
            ("Type", true) => groups.OrderByDescending(g => g.Type).ThenBy(g => g.Name),
            ("Members", false) => groups.OrderBy(g => g.Members.Count).ThenBy(g => g.Name),
            ("Members", true) => groups.OrderByDescending(g => g.Members.Count).ThenBy(g => g.Name),
            (_, true) => groups.OrderByDescending(g => g.Name),
            _ => groups.OrderBy(g => g.Name),
        };

        var total = await groups.CountAsync(cancellationToken);
        var rows = await groups.Skip(page.Skip).Take(page.PageSize)
            .Select(g => new { g.Id, g.Name, g.Description, g.Type, g.Email, g.ManagerId, MemberCount = g.Members.Count, g.Source, g.IsActive })
            .ToListAsync(cancellationToken);
        var ids = rows.Select(r => r.Id).ToList();
        var managerIds = rows.Where(r => r.ManagerId.HasValue).Select(r => r.ManagerId!.Value).Distinct().ToList();
        var managers = await db.Set<User>().AsNoTracking().Where(u => managerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);
        var roles = await db.Set<GroupRole>().AsNoTracking().Where(r => ids.Contains(r.GroupId)).Select(r => new { r.GroupId, r.Role!.Label }).ToListAsync(cancellationToken);
        var rolesByGroup = roles.GroupBy(r => r.GroupId).ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Label).OrderBy(l => l).ToList());

        var items = rows.Select(r => new GroupListItem(
            r.Id, r.Name, r.Description, r.Type, r.Email, r.ManagerId,
            r.ManagerId is { } m ? managers.GetValueOrDefault(m) : null,
            r.MemberCount, rolesByGroup.GetValueOrDefault(r.Id) ?? [], r.Source, r.IsActive)).ToList();
        return Result<PagedResult<GroupListItem>>.Success(new PagedResult<GroupListItem>(items, total, page.Page, page.PageSize));
    }

    /// <inheritdoc />
    public async Task<Result<GroupStats>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ReadUsers, cancellationToken))
        {
            return Result<GroupStats>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var groups = db.Set<Group>().AsNoTracking();
        var stats = new GroupStats(
            await groups.CountAsync(cancellationToken),
            await groups.CountAsync(g => g.Type == GroupType.Assignment, cancellationToken),
            await groups.CountAsync(g => g.Type == GroupType.Approval, cancellationToken),
            await groups.CountAsync(g => g.Type == GroupType.Security, cancellationToken),
            await db.Set<GroupMember>().CountAsync(cancellationToken),
            await groups.CountAsync(g => g.IsActive && !g.Members.Any(), cancellationToken));
        return Result<GroupStats>.Success(stats);
    }

    /// <inheritdoc />
    public async Task<Result<GroupDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ReadUsers, cancellationToken))
        {
            return Result<GroupDetail>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var group = await db.Set<Group>().AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (group is null)
        {
            return Result<GroupDetail>.NotFound();
        }

        var memberQuery = db.Set<GroupMember>().AsNoTracking().Where(m => m.GroupId == id);
        var memberCount = await memberQuery.CountAsync(cancellationToken);
        var members = await memberQuery
            .OrderBy(m => m.User!.DisplayName)
            .Take(MaxMembersShown)
            .Select(m => new GroupMemberItem(m.UserId, m.User!.DisplayName, m.User.UserName, m.User.Email, m.User.Title, m.User.Source, m.User.IsActive, m.IsFromDirectory))
            .ToListAsync(cancellationToken);
        var roles = await db.Set<GroupRole>().AsNoTracking().Where(r => r.GroupId == id)
            .Select(r => new GroupRoleItem(r.RoleId, r.Role!.Name, r.Role.Label, r.Role.IsSystem, r.Role.Permissions.Count))
            .ToListAsync(cancellationToken);
        var parentName = group.ParentId is { } parentId
            ? await db.Set<Group>().AsNoTracking().Where(g => g.Id == parentId).Select(g => g.Name).FirstOrDefaultAsync(cancellationToken)
            : null;
        var mappings = await db.Set<DirectoryGroupMapping>().AsNoTracking().Where(m => m.GroupId == id).Select(m => m.DirectoryGroup).Take(50).ToListAsync(cancellationToken);

        return Result<GroupDetail>.Success(new GroupDetail
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            Email = group.Email,
            Type = group.Type,
            ManagerId = group.ManagerId,
            ParentId = group.ParentId,
            ParentName = parentName,
            Source = group.Source,
            DistinguishedName = group.DistinguishedName,
            IsActive = group.IsActive,
            CreatedAt = group.CreatedAt,
            CreatedBy = group.CreatedBy,
            UpdatedAt = group.UpdatedAt,
            UpdatedBy = group.UpdatedBy,
            Version = group.Version,
            Members = members,
            MemberCount = memberCount,
            Roles = roles.OrderBy(r => r.Label).ToList(),
            DirectoryMappings = mappings,
        });
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateAsync(GroupEditModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ManageGroups, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        if (Validate(model) is { } error)
        {
            return Result<Guid>.Invalid(error);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var name = model.Name.Trim();
        if (await db.Set<Group>().AnyAsync(g => g.Name == name, cancellationToken))
        {
            return Result<Guid>.Conflict("A group with this name already exists.");
        }

        if (await ValidateReferencesAsync(db, model, null, cancellationToken) is { } referenceError)
        {
            return Result<Guid>.Invalid(referenceError);
        }

        var group = new Group();
        Apply(group, model);
        db.Set<Group>().Add(group);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry("identity", "group.created")
        {
            TargetType = "sys_user_group",
            TargetId = group.Id.ToString(),
            TargetName = group.Name,
            Details = new { group.Type },
        }, cancellationToken);
        return Result<Guid>.Success(group.Id);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync(Guid id, GroupEditModel model, long version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ManageGroups, cancellationToken))
        {
            return Result.Forbidden();
        }

        if (Validate(model) is { } error)
        {
            return Result.Invalid(error);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var group = await db.Set<Group>().FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (group is null)
        {
            return Result.NotFound();
        }

        if (group.Version != version)
        {
            return Result.Conflict("The group was changed by someone else. Reload the page and apply your changes again.");
        }

        var name = model.Name.Trim();
        if (await db.Set<Group>().AnyAsync(g => g.Id != id && g.Name == name, cancellationToken))
        {
            return Result.Conflict("A group with this name already exists.");
        }

        if (await ValidateReferencesAsync(db, model, id, cancellationToken) is { } referenceError)
        {
            return Result.Invalid(referenceError);
        }

        var activeChanged = group.IsActive != model.IsActive;
        if (activeChanged && !model.IsActive && await WouldRemoveLastAdminAsync(db, id, null, cancellationToken))
        {
            return Result.Invalid(SuperAdminGuard.LastAdministratorMessage);
        }

        if (activeChanged && model.IsActive)
        {
            // Reactivating a group hands its roles to all members again.
            var guard = await GuardGroupGrantsAsync(db, id, cancellationToken);
            if (!guard.Succeeded)
            {
                return guard;
            }
        }

        var before = new { group.Name, group.Description, group.Email, group.Type, group.ManagerId, group.ParentId, group.IsActive };
        Apply(group, model);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict("The group was changed by someone else. Reload the page and apply your changes again.");
        }

        if (activeChanged)
        {
            permissions.Invalidate();
        }

        await audit.WriteAsync(new AuditEntry("identity", "group.updated")
        {
            TargetType = "sys_user_group",
            TargetId = group.Id.ToString(),
            TargetName = group.Name,
            Details = new { before, after = new { group.Name, group.Description, group.Email, group.Type, group.ManagerId, group.ParentId, group.IsActive } },
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ManageGroups, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var group = await db.Set<Group>().FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (group is null)
        {
            return Result.NotFound();
        }

        if (await WouldRemoveLastAdminAsync(db, id, null, cancellationToken))
        {
            return Result.Invalid(SuperAdminGuard.LastAdministratorMessage);
        }

        // Detach children and mappings that point to the group (no database-level foreign keys for these references).
        foreach (var child in await db.Set<Group>().Where(g => g.ParentId == id).ToListAsync(cancellationToken))
        {
            child.ParentId = null;
        }

        foreach (var mapping in await db.Set<DirectoryGroupMapping>().Where(m => m.GroupId == id).ToListAsync(cancellationToken))
        {
            mapping.GroupId = null;
            if (mapping.RoleId is null)
            {
                db.Set<DirectoryGroupMapping>().Remove(mapping);
            }
        }

        db.Set<Group>().Remove(group);
        await db.SaveChangesAsync(cancellationToken);
        permissions.Invalidate();
        await audit.WriteAsync(new AuditEntry("identity", "group.deleted")
        {
            TargetType = "sys_user_group",
            TargetId = group.Id.ToString(),
            TargetName = group.Name,
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<int>> AddMembersAsync(Guid groupId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ManageGroups, cancellationToken))
        {
            return Result<int>.Forbidden();
        }

        if (userIds.Count == 0 || userIds.Count > MaxMembersPerCall)
        {
            return Result<int>.Invalid($"Select between 1 and {MaxMembersPerCall} users.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var group = await db.Set<Group>().AsNoTracking().Where(g => g.Id == groupId).Select(g => new { g.Id, g.Name }).FirstOrDefaultAsync(cancellationToken);
        if (group is null)
        {
            return Result<int>.NotFound();
        }

        var guard = await GuardGroupGrantsAsync(db, groupId, cancellationToken);
        if (!guard.Succeeded)
        {
            return Result<int>.From(guard);
        }

        var ids = userIds.Distinct().ToList();
        var existingUsers = await db.Set<User>().AsNoTracking().Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.UserName }).ToListAsync(cancellationToken);
        var alreadyMembers = await db.Set<GroupMember>().Where(m => m.GroupId == groupId && ids.Contains(m.UserId)).ToListAsync(cancellationToken);
        var added = new List<string>();
        foreach (var user in existingUsers)
        {
            var existing = alreadyMembers.FirstOrDefault(m => m.UserId == user.Id);
            if (existing is null)
            {
                db.Set<GroupMember>().Add(new GroupMember { GroupId = groupId, UserId = user.Id });
                added.Add(user.UserName);
            }
            else if (existing.IsFromDirectory)
            {
                // Make the membership manual so it survives directory changes.
                existing.IsFromDirectory = false;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        foreach (var user in existingUsers)
        {
            permissions.Invalidate(user.Id);
        }

        if (added.Count > 0)
        {
            await audit.WriteAsync(new AuditEntry("identity", "group.members_added")
            {
                TargetType = "sys_user_group",
                TargetId = group.Id.ToString(),
                TargetName = group.Name,
                Details = new { users = added },
            }, cancellationToken);
        }

        return Result<int>.Success(added.Count);
    }

    /// <inheritdoc />
    public async Task<Result> RemoveMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ManageGroups, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var membership = await db.Set<GroupMember>().Include(m => m.Group).Include(m => m.User).FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId, cancellationToken);
        if (membership is null)
        {
            return Result.NotFound("The user is not a member of this group.");
        }

        if (await WouldRemoveLastAdminAsync(db, groupId, userId, cancellationToken))
        {
            return Result.Invalid(SuperAdminGuard.LastAdministratorMessage);
        }

        db.Set<GroupMember>().Remove(membership);
        await db.SaveChangesAsync(cancellationToken);
        permissions.Invalidate(userId);
        await audit.WriteAsync(new AuditEntry("identity", "group.member_removed")
        {
            TargetType = "sys_user_group",
            TargetId = groupId.ToString(),
            TargetName = membership.Group?.Name,
            Details = new { user = membership.User?.UserName, membership.IsFromDirectory },
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> AddRoleAsync(Guid groupId, Guid roleId, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ManageGroups, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var group = await db.Set<Group>().AsNoTracking().Where(g => g.Id == groupId).Select(g => new { g.Id, g.Name }).FirstOrDefaultAsync(cancellationToken);
        var role = await db.Set<Role>().AsNoTracking().Where(r => r.Id == roleId).Select(r => new { r.Id, r.Name }).FirstOrDefaultAsync(cancellationToken);
        if (group is null || role is null)
        {
            return Result.NotFound();
        }

        var requested = await IdentityGrants.GetRolePermissionsAsync(db, [roleId], cancellationToken);
        var missing = await PrivilegeEscalationGuard.FindUngrantedAsync(currentUser.HasPermissionAsync, requested, cancellationToken);
        if (missing.Count > 0)
        {
            await AuditDeniedAsync(group.Id, group.Name, role.Name, missing, cancellationToken);
            return Result.Forbidden(PrivilegeEscalationGuard.Describe(missing));
        }

        if (await db.Set<GroupRole>().AnyAsync(r => r.GroupId == groupId && r.RoleId == roleId, cancellationToken))
        {
            return Result.Success();
        }

        db.Set<GroupRole>().Add(new GroupRole { GroupId = groupId, RoleId = roleId });
        await db.SaveChangesAsync(cancellationToken);
        permissions.Invalidate();
        await audit.WriteAsync(new AuditEntry("identity", "group.role_granted")
        {
            TargetType = "sys_user_group",
            TargetId = group.Id.ToString(),
            TargetName = group.Name,
            Details = new { role = role.Name },
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> RemoveRoleAsync(Guid groupId, Guid roleId, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ManageGroups, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var grant = await db.Set<GroupRole>().Include(r => r.Role).FirstOrDefaultAsync(r => r.GroupId == groupId && r.RoleId == roleId, cancellationToken);
        if (grant is null)
        {
            return Result.NotFound("The role is not granted by this group.");
        }

        var grantsAll = await db.Set<RolePermission>().AnyAsync(p => p.RoleId == roleId && p.Permission == Permissions.All, cancellationToken);
        if (grantsAll && await WouldRemoveLastAdminAsync(db, groupId, null, cancellationToken))
        {
            return Result.Invalid(SuperAdminGuard.LastAdministratorMessage);
        }

        db.Set<GroupRole>().Remove(grant);
        await db.SaveChangesAsync(cancellationToken);
        permissions.Invalidate();
        await audit.WriteAsync(new AuditEntry("identity", "group.role_revoked")
        {
            TargetType = "sys_user_group",
            TargetId = groupId.ToString(),
            Details = new { role = grant.Role?.Name },
        }, cancellationToken);
        return Result.Success();
    }

    private static string? Validate(GroupEditModel model)
    {
        var name = model.Name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 200)
        {
            return "The group name must be between 2 and 200 characters.";
        }

        if (model.Description is { Length: > 2000 })
        {
            return "The description must not exceed 2000 characters.";
        }

        if (!string.IsNullOrWhiteSpace(model.Email) && (model.Email.Trim().Length > 320 || !EmailRegex().IsMatch(model.Email.Trim())))
        {
            return "Enter a valid group e-mail address.";
        }

        return Enum.IsDefined(model.Type) ? null : "Select a valid group type.";
    }

    private static void Apply(Group group, GroupEditModel model)
    {
        group.Name = model.Name.Trim();
        group.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        group.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
        group.Type = model.Type;
        group.ManagerId = model.ManagerId;
        group.ParentId = model.ParentId;
        group.IsActive = model.IsActive;
    }

    private static async Task<string?> ValidateReferencesAsync(IAppDbContext db, GroupEditModel model, Guid? groupId, CancellationToken cancellationToken)
    {
        if (model.ManagerId is { } managerId && !await db.Set<User>().AnyAsync(u => u.Id == managerId, cancellationToken))
        {
            return "The selected manager does not exist.";
        }

        if (model.ParentId is not { } parentId)
        {
            return null;
        }

        if (parentId == groupId)
        {
            return "A group cannot be its own parent.";
        }

        // Walk up the hierarchy (bounded) to prevent cycles.
        Guid? current = parentId;
        for (var depth = 0; current is { } cursor && depth < 32; depth++)
        {
            var parent = await db.Set<Group>().AsNoTracking().Where(g => g.Id == cursor).Select(g => new { g.Id, g.ParentId }).FirstOrDefaultAsync(cancellationToken);
            if (parent is null)
            {
                return depth == 0 ? "The selected parent group does not exist." : null;
            }

            if (groupId is not null && parent.ParentId == groupId)
            {
                return "The selected parent would create a cycle in the group hierarchy.";
            }

            current = parent.ParentId;
        }

        return null;
    }

    private static async Task<bool> WouldRemoveLastAdminAsync(IAppDbContext db, Guid groupId, Guid? onlyUserId, CancellationToken cancellationToken)
    {
        var groupGrantsAll = await db.Set<GroupRole>().AsNoTracking()
            .Where(r => r.GroupId == groupId)
            .AnyAsync(r => db.Set<RolePermission>().Any(p => p.RoleId == r.RoleId && p.Permission == Permissions.All), cancellationToken);
        if (!groupGrantsAll)
        {
            return false;
        }

        var admins = await SuperAdminGuard.GetActiveSuperAdminIdsAsync(db, cancellationToken);
        IReadOnlyList<Guid> affected = onlyUserId is { } single
            ? [single]
            : await IdentityGrants.GetGroupMemberIdsAsync(db, groupId, cancellationToken);
        var affectedAdmins = admins.Intersect(affected).ToList();
        if (affectedAdmins.Count == 0 || admins.Count > affectedAdmins.Count)
        {
            return false;
        }

        // Every remaining administrator would lose access unless they hold the wildcard directly.
        var directAdmins = await db.Set<UserRole>().AsNoTracking()
            .Where(r => affectedAdmins.Contains(r.UserId))
            .Where(r => db.Set<RolePermission>().Any(p => p.RoleId == r.RoleId && p.Permission == Permissions.All))
            .Select(r => r.UserId)
            .AnyAsync(cancellationToken);
        return !directAdmins;
    }

    private async Task<Result> GuardGroupGrantsAsync(IAppDbContext db, Guid groupId, CancellationToken cancellationToken)
    {
        var requested = await IdentityGrants.GetGroupPermissionsAsync(db, groupId, cancellationToken);
        var missing = await PrivilegeEscalationGuard.FindUngrantedAsync(currentUser.HasPermissionAsync, requested, cancellationToken);
        if (missing.Count == 0)
        {
            return Result.Success();
        }

        await AuditDeniedAsync(groupId, null, null, missing, cancellationToken);
        return Result.Forbidden(PrivilegeEscalationGuard.Describe(missing));
    }

    private Task AuditDeniedAsync(Guid groupId, string? groupName, string? role, IReadOnlyList<string> missing, CancellationToken cancellationToken) =>
        audit.WriteAsync(new AuditEntry("identity", "group.grant_denied", AuditOutcome.Denied)
        {
            TargetType = "sys_user_group",
            TargetId = groupId.ToString(),
            TargetName = groupName,
            Message = "Privilege escalation prevented.",
            Details = new { role, missing },
        }, cancellationToken);

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();
}
